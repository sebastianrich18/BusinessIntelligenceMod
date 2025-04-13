// Business Intelligence Mod for Schedule I
// Copyright (c) 2025 [Your Name]
// Licensed under the MIT License - see LICENSE file for details

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using ScheduleOne.Economy;
using ScheduleOne.Quests;
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
using ScheduleOne.UI.Handover;
using ScheduleOne.GameTime;
using FishNet.Object;

namespace BusinessIntelligenceMod
{
    public class BusinessIntelligenceMod : MelonMod
    {
        public static readonly string ModDataPath = Path.Combine(Application.persistentDataPath, "BusinessIntelligence");
        public static BusinessIntelligenceMod Instance;

        // Path to the single CSV file
        private string dataLogPath;

        // CSV Header
        private const string DATA_LOG_HEADER = "GameTime,RealTime,EventType,Payload";

        // Event types
        private const string EVENT_SALE = "SALE";
        private const string EVENT_OFFER_ACCEPTED = "OFFER_ACCEPTED";
        private const string EVENT_OFFER_REJECTED = "OFFER_REJECTED";
        private const string EVENT_COUNTER_OFFER = "COUNTER_OFFER";
        private const string EVENT_CUSTOMER_PREFERENCE = "CUSTOMER_PREFERENCE";
        private const string EVENT_OFFER_CHANCE = "OFFER_CHANCE";

        // Time tracking
        private DateTime modStartTime;

        // Helper dictionary for caching offer success chances
        private Dictionary<int, OfferChanceData> offerChanceCache = new Dictionary<int, OfferChanceData>();

        [Obsolete]
        public override void OnApplicationStart()
        {
            Instance = this;
            modStartTime = DateTime.Now;

            try
            {
                // Create data directory
                Directory.CreateDirectory(ModDataPath);

                // Initialize file path for the single CSV
                dataLogPath = Path.Combine(ModDataPath, "business_intelligence_data.csv");

                // Create CSV file with header if it doesn't exist
                InitializeCSVFile();

                // Apply Harmony patches
                HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("com.yourusername.businessintelligence");

                // Log all customer methods to help with debugging
                LogCustomerMethods();

                // Patch methods for tracking
                var customerType = typeof(Customer);

                // Sale tracking
                PatchMethod(harmony, customerType, "ProcessHandoverServerSide", nameof(ProcessHandoverServerSide_Postfix));

                // Offer tracking
                PatchMethod(harmony, customerType, "GetOfferSuccessChance", nameof(GetOfferSuccessChance_Postfix));
                PatchMethod(harmony, customerType, "PlayerAcceptedContract", nameof(PlayerAcceptedContract_Postfix));
                PatchMethod(harmony, customerType, "ContractRejected", nameof(ContractRejected_Postfix));

                // Counter-offer tracking
                PatchMethod(harmony, customerType, "ProcessCounterOfferServerSide", nameof(ProcessCounterOfferServerSide_Postfix));

                // Delivery evaluation
                PatchMethod(harmony, customerType, "EvaluateDelivery", nameof(EvaluateDelivery_Postfix));

                MelonLogger.Msg("Business Intelligence Mod initialized!");
                MelonLogger.Msg($"Data will be saved to: {dataLogPath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error during initialization: {ex}");
            }
        }

        private void InitializeCSVFile()
        {
            try
            {
                // Create CSV file with header if it doesn't exist
                if (!File.Exists(dataLogPath))
                {
                    File.WriteAllText(dataLogPath, DATA_LOG_HEADER + Environment.NewLine);
                    MelonLogger.Msg($"Created data log file at {dataLogPath}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initializing CSV file: {ex.Message}");
            }
        }

        private void PatchMethod(HarmonyLib.Harmony harmony, Type targetType, string methodName, string patchMethodName)
        {
            try
            {
                var original = AccessTools.Method(targetType, methodName);
                if (original != null)
                {
                    var postfix = AccessTools.Method(typeof(BusinessIntelligenceMod), patchMethodName);
                    harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg($"Successfully patched {methodName}");
                }
                else
                {
                    MelonLogger.Warning($"Method {methodName} not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error patching {methodName}: {ex.Message}");
            }
        }

        private void LogCustomerMethods()
        {
            try
            {
                // Log all public methods of Customer class to help with debugging
                var methods = typeof(Customer).GetMethods(System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.NonPublic |
                                                       System.Reflection.BindingFlags.Instance);

                string methodsFile = Path.Combine(ModDataPath, "customer_methods.txt");
                using (StreamWriter writer = new StreamWriter(methodsFile))
                {
                    writer.WriteLine("Customer Class Methods:");
                    foreach (var method in methods)
                    {
                        writer.WriteLine($"Method: {method.Name}");
                        writer.WriteLine($"  Return Type: {method.ReturnType.Name}");
                        writer.WriteLine($"  Parameters:");
                        foreach (var param in method.GetParameters())
                        {
                            writer.WriteLine($"    {param.ParameterType.Name} {param.Name}");
                        }
                        writer.WriteLine();
                    }
                }

                MelonLogger.Msg($"Logged Customer methods to {methodsFile}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error logging Customer methods: {ex}");
            }
        }

        // Helper method to log events to the CSV file
        private void LogEvent(string eventType, string payload)
        {
            try
            {
                string gameTime = DateTime.Now.ToString("HH:mm:ss");
                string realTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Escape any commas in the payload with quotes to maintain CSV integrity
                string escapedPayload = $"\"{payload.Replace("\"", "\"\"")}\"";

                string logEntry = $"{gameTime},{realTime},{eventType},{escapedPayload}";

                // Append to the CSV file
                File.AppendAllText(dataLogPath, logEntry + Environment.NewLine);

                MelonLogger.Msg($"Logged {eventType} event");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error logging event: {ex.Message}");
            }
        }

        // Helper method to classify product types based on ID 
        private static string ClassifyProductType(string productID)
        {
            // Convert to lowercase for case-insensitive matching
            productID = productID.ToLower();

            // Weed products
            if (productID.Contains("purple") ||
                productID.Contains("kush") ||
                productID.Contains("haze") ||
                productID.Contains("sativa") ||
                productID.Contains("indica") ||
                productID.Contains("og") ||
                productID.Contains("skunk"))
            {
                return "weed";
            }

            // Meth products
            if (productID.Contains("meth") ||
                productID.Contains("crystal") ||
                productID.Contains("ice") ||
                productID.Contains("glass") ||
                productID.Contains("blue"))
            {
                return "meth";
            }

            // Cocaine products
            if (productID.Contains("cocaine") ||
                productID.Contains("coke") ||
                productID.Contains("snow") ||
                productID.Contains("blow") ||
                productID.Contains("white"))
            {
                return "cocaine";
            }

            return "unknown";
        }

        // Helper method to create a stringified payload for CSV
        private static string CreatePayload(params KeyValuePair<string, string>[] pairs)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");

            for (int i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i];
                sb.Append($"\"{pair.Key}\":\"{pair.Value.Replace("\"", "\\\"")}\"");

                if (i < pairs.Length - 1)
                    sb.Append(",");
            }

            sb.Append("}");
            return sb.ToString();
        }

        // Harmony patch methods for tracking various aspects

        // Sale tracking
        public static void ProcessHandoverServerSide_Postfix(Customer __instance, HandoverScreen.EHandoverOutcome outcome,
                                                            List<ItemInstance> items, bool handoverByPlayer,
                                                            float totalPayment, ProductList productList,
                                                            float satisfaction, NetworkObject dealer)
        {
            try
            {
                if (outcome != HandoverScreen.EHandoverOutcome.Finalize)
                    return;

                MelonLogger.Msg("ProcessHandoverServerSide_Postfix executed!");

                // Get customer information
                string customerName = "Unknown";
                if (__instance.NPC != null)
                {
                    customerName = __instance.NPC.fullName;
                }

                // Get dealer information
                string dealerName = handoverByPlayer ? "Player" : "Unknown";
                if (dealer != null)
                {
                    // Just log the dealer object name since we can't access NPC property
                    dealerName = dealer.name;
                }

                // Process items
                int totalQuantityRequested = 0;
                int totalQuantityProvided = 0;
                string itemIDs = "";
                string itemTypes = "";

                if (productList != null && productList.entries.Count > 0)
                {
                    foreach (var entry in productList.entries)
                    {
                        totalQuantityRequested += entry.Quantity;
                        string productType = ClassifyProductType(entry.ProductID);
                        itemIDs += $"{entry.ProductID}({entry.Quantity});";
                        itemTypes += $"{productType};";
                    }
                }

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        totalQuantityProvided += item.Quantity;
                    }
                }

                // Create payload for sale event
                string payload = CreatePayload(
                    new KeyValuePair<string, string>("customer", customerName),
                    new KeyValuePair<string, string>("dealer", dealerName),
                    new KeyValuePair<string, string>("handoverByPlayer", handoverByPlayer.ToString()),
                    new KeyValuePair<string, string>("payment", totalPayment.ToString("F2")),
                    new KeyValuePair<string, string>("satisfaction", satisfaction.ToString("F4")),
                    new KeyValuePair<string, string>("quantityRequested", totalQuantityRequested.ToString()),
                    new KeyValuePair<string, string>("quantityProvided", totalQuantityProvided.ToString()),
                    new KeyValuePair<string, string>("itemIDs", itemIDs),
                    new KeyValuePair<string, string>("itemTypes", itemTypes)
                );

                // Log the sale event
                Instance.LogEvent(EVENT_SALE, payload);

                // Also track customer preferences after a sale
                if (__instance.CurrentAddiction > 0)
                {
                    string mainDrugType = "Unknown";
                    // If we can get the main drug type from the product list
                    if (productList != null && productList.entries.Count > 0)
                    {
                        mainDrugType = ClassifyProductType(productList.entries[0].ProductID);
                    }

                    // Create payload for customer preference
                    string prefsPayload = CreatePayload(
                        new KeyValuePair<string, string>("customer", customerName),
                        new KeyValuePair<string, string>("currentAddiction", __instance.CurrentAddiction.ToString("F4")),
                        new KeyValuePair<string, string>("highestAddiction", "0"), // Placeholder
                        new KeyValuePair<string, string>("mainDrugType", mainDrugType),
                        new KeyValuePair<string, string>("source", "Sale")
                    );

                    // Log the customer preference event
                    Instance.LogEvent(EVENT_CUSTOMER_PREFERENCE, prefsPayload);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ProcessHandoverServerSide_Postfix: {ex.Message}");
            }
        }

        // Success chance calculation tracking
        public static void GetOfferSuccessChance_Postfix(Customer __instance, List<ItemInstance> items, float askingPrice, ref float __result)
        {
            try
            {
                // Store the calculated chance with the customerID as the key
                int customerID = __instance.GetInstanceID();

                // Log the details of this calculation
                string customerName = "Unknown";
                if (__instance.NPC != null)
                {
                    customerName = __instance.NPC.fullName;
                }

                string itemsDetails = "";
                float totalQuantity = 0;
                string itemTypes = "";

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        string productType = ClassifyProductType(item.ID);
                        itemsDetails += $"{item.ID}({item.Quantity});";
                        itemTypes += $"{productType};";
                        totalQuantity += item.Quantity;
                    }
                }

                // If we're offering something with defined price, keep it for logging
                if (askingPrice > 0 && totalQuantity > 0)
                {
                    // Fix the cache issue by storing all data needed
                    if (!Instance.offerChanceCache.ContainsKey(customerID))
                    {
                        Instance.offerChanceCache[customerID] = new OfferChanceData
                        {
                            Timestamp = DateTime.Now,
                            CustomerName = customerName,
                            Items = items,
                            AskingPrice = askingPrice,
                            SuccessChance = __result
                        };
                    }
                    else
                    {
                        // Update the existing entry
                        Instance.offerChanceCache[customerID].Timestamp = DateTime.Now;
                        Instance.offerChanceCache[customerID].CustomerName = customerName;
                        Instance.offerChanceCache[customerID].Items = new List<ItemInstance>(items);
                        Instance.offerChanceCache[customerID].AskingPrice = askingPrice;
                        Instance.offerChanceCache[customerID].SuccessChance = __result;
                    }

                    // Create payload for offer chance calculation
                    string payload = CreatePayload(
                        new KeyValuePair<string, string>("customer", customerName),
                        new KeyValuePair<string, string>("items", itemsDetails),
                        new KeyValuePair<string, string>("itemTypes", itemTypes),
                        new KeyValuePair<string, string>("askingPrice", askingPrice.ToString("F2")),
                        new KeyValuePair<string, string>("totalQuantity", totalQuantity.ToString()),
                        new KeyValuePair<string, string>("successChance", __result.ToString("F4"))
                    );

                    // Log the offer chance event
                    Instance.LogEvent(EVENT_OFFER_CHANCE, payload);

                    // Log the cached value to verify it's working
                    MelonLogger.Msg($"Cached success chance: {__result:F4} for customer {customerName} (ID: {customerID})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in GetOfferSuccessChance_Postfix: {ex.Message}");
            }
        }

        // Track accepted offers
        public static void PlayerAcceptedContract_Postfix(Customer __instance, ScheduleOne.Economy.EDealWindow window)
        {
            try
            {
                if (__instance.OfferedContractInfo == null || __instance.OfferedContractInfo.Products.entries.Count == 0)
                    return;

                string customerName = "Unknown";
                if (__instance.NPC != null)
                {
                    customerName = __instance.NPC.fullName;
                }

                var entry = __instance.OfferedContractInfo.Products.entries[0];
                string productType = ClassifyProductType(entry.ProductID);

                // Try to get cached success chance
                float successChance = -1;
                int customerID = __instance.GetInstanceID();

                if (Instance.offerChanceCache.ContainsKey(customerID))
                {
                    successChance = Instance.offerChanceCache[customerID].SuccessChance;
                    MelonLogger.Msg($"Retrieved cached success chance: {successChance:F4} for customer {customerName} (ID: {customerID})");
                }
                else
                {
                    MelonLogger.Warning($"No cached success chance found for customer {customerName} (ID: {customerID})");
                }

                // Create payload for accepted offer
                string payload = CreatePayload(
                    new KeyValuePair<string, string>("customer", customerName),
                    new KeyValuePair<string, string>("productID", entry.ProductID),
                    new KeyValuePair<string, string>("productType", productType),
                    new KeyValuePair<string, string>("quantity", entry.Quantity.ToString()),
                    new KeyValuePair<string, string>("price", __instance.OfferedContractInfo.Payment.ToString("F2")),
                    new KeyValuePair<string, string>("window", window.ToString()),
                    new KeyValuePair<string, string>("successChance", successChance.ToString("F4"))
                );

                // Log the accepted offer event
                Instance.LogEvent(EVENT_OFFER_ACCEPTED, payload);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PlayerAcceptedContract_Postfix: {ex.Message}");
            }
        }

        // Track rejected offers
        public static void ContractRejected_Postfix(Customer __instance)
        {
            try
            {
                if (__instance.OfferedContractInfo == null || __instance.OfferedContractInfo.Products.entries.Count == 0)
                    return;

                string customerName = "Unknown";
                if (__instance.NPC != null)
                {
                    customerName = __instance.NPC.fullName;
                }

                var entry = __instance.OfferedContractInfo.Products.entries[0];
                string productType = ClassifyProductType(entry.ProductID);

                // Try to get cached success chance
                float successChance = -1;
                int customerID = __instance.GetInstanceID();

                if (Instance.offerChanceCache.ContainsKey(customerID))
                {
                    successChance = Instance.offerChanceCache[customerID].SuccessChance;
                    MelonLogger.Msg($"Retrieved cached success chance: {successChance:F4} for customer {customerName} (ID: {customerID})");
                }
                else
                {
                    MelonLogger.Warning($"No cached success chance found for customer {customerName} (ID: {customerID})");
                }

                // Create payload for rejected offer
                string payload = CreatePayload(
                    new KeyValuePair<string, string>("customer", customerName),
                    new KeyValuePair<string, string>("productID", entry.ProductID),
                    new KeyValuePair<string, string>("productType", productType),
                    new KeyValuePair<string, string>("quantity", entry.Quantity.ToString()),
                    new KeyValuePair<string, string>("price", __instance.OfferedContractInfo.Payment.ToString("F2")),
                    new KeyValuePair<string, string>("successChance", successChance.ToString("F4"))
                );

                // Log the rejected offer event
                Instance.LogEvent(EVENT_OFFER_REJECTED, payload);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ContractRejected_Postfix: {ex.Message}");
            }
        }

        // Track counter offers
        public static void ProcessCounterOfferServerSide_Postfix(Customer __instance, string productID, int quantity, float price)
        {
            try
            {
                if (__instance.OfferedContractInfo == null || __instance.OfferedContractInfo.Products.entries.Count == 0)
                    return;

                string customerName = "Unknown";
                if (__instance.NPC != null)
                {
                    customerName = __instance.NPC.fullName;
                }

                var originalEntry = __instance.OfferedContractInfo.Products.entries[0];
                string originalProductType = ClassifyProductType(originalEntry.ProductID);
                string counterProductType = ClassifyProductType(productID);

                // We can't call EvaluateCounteroffer directly as it's protected
                // Try to infer acceptance based on later behavior
                bool accepted = true; // Assume accepted for now

                // Create payload for counter offer
                string payload = CreatePayload(
                    new KeyValuePair<string, string>("customer", customerName),
                    new KeyValuePair<string, string>("originalProductID", originalEntry.ProductID),
                    new KeyValuePair<string, string>("originalProductType", originalProductType),
                    new KeyValuePair<string, string>("originalQuantity", originalEntry.Quantity.ToString()),
                    new KeyValuePair<string, string>("originalPrice", __instance.OfferedContractInfo.Payment.ToString("F2")),
                    new KeyValuePair<string, string>("counterProductID", productID),
                    new KeyValuePair<string, string>("counterProductType", counterProductType),
                    new KeyValuePair<string, string>("counterQuantity", quantity.ToString()),
                    new KeyValuePair<string, string>("counterPrice", price.ToString("F2")),
                    new KeyValuePair<string, string>("accepted", accepted.ToString())
                );

                // Log the counter offer event
                Instance.LogEvent(EVENT_COUNTER_OFFER, payload);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ProcessCounterOfferServerSide_Postfix: {ex.Message}");
            }
        }

        // Track delivery evaluation details - with better error handling
        public static void EvaluateDelivery_Postfix(Customer __instance, Contract contract, List<ItemInstance> providedItems,
                                                   ref float highestAddiction, ref EDrugType mainDrugType,
                                                   ref int matchedProductCount, float __result)
        {
            try
            {
                string customerName = "Unknown";
                if (__instance.NPC != null)
                {
                    customerName = __instance.NPC.fullName;
                }

                // Store the drug type information for future lookup
                string mainDrugTypeStr = mainDrugType.ToString();

                // Create payload for customer preference from delivery
                string payload = CreatePayload(
                    new KeyValuePair<string, string>("customer", customerName),
                    new KeyValuePair<string, string>("currentAddiction", __instance.CurrentAddiction.ToString("F4")),
                    new KeyValuePair<string, string>("highestAddiction", highestAddiction.ToString("F4")),
                    new KeyValuePair<string, string>("mainDrugType", mainDrugTypeStr),
                    new KeyValuePair<string, string>("matchedProductCount", matchedProductCount.ToString()),
                    new KeyValuePair<string, string>("satisfaction", __result.ToString("F4")),
                    new KeyValuePair<string, string>("source", "Delivery")
                );

                // Log the customer preference event
                Instance.LogEvent(EVENT_CUSTOMER_PREFERENCE, payload);

                // Log the detailed evaluation info
                MelonLogger.Msg($"Delivery evaluation for {customerName}: " +
                               $"Satisfaction={__result}, HighestAddiction={highestAddiction}, " +
                               $"MainDrugType={mainDrugTypeStr}, MatchedProducts={matchedProductCount}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in EvaluateDelivery_Postfix: {ex.Message}");
            }
        }

        // Helper class for caching offer chances
        private class OfferChanceData
        {
            public DateTime Timestamp;
            public string CustomerName;
            public List<ItemInstance> Items;
            public float AskingPrice;
            public float SuccessChance;
        }
    }
}