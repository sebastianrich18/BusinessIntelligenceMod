// Business Intelligence Mod for Schedule I
// Copyright (c) 2025 [Your Name]
// Licensed under the MIT License - see LICENSE file for details

using System;
using System.Collections.Generic;
using System.IO;
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

        // Tracking lists for different types of data
        private List<string> salesLog = new List<string>();
        private List<string> offerLog = new List<string>();
        private List<string> counterOfferLog = new List<string>();
        private List<string> customerPreferencesLog = new List<string>();

        // CSV Headers
        private const string SALES_HEADER = "DateTime,GameTime,Customer,Dealer,HandoverByPlayer,Payment,Satisfaction,QuantityRequested,QuantityProvided,ItemIDs,ItemTypes";
        private const string OFFERS_HEADER = "DateTime,GameTime,Customer,ProductID,ProductType,Quantity,Price,Window,SuccessChance,Accepted";
        private const string COUNTER_OFFERS_HEADER = "DateTime,GameTime,Customer,OriginalProductID,OriginalProductType,OriginalQuantity,OriginalPrice,CounterProductID,CounterProductType,CounterQuantity,CounterPrice,Accepted";
        private const string CUSTOMER_PREFERENCES_HEADER = "DateTime,GameTime,Customer,CurrentAddiction,HighestAddiction,MainDrugType,Source";

        // CSV File paths
        private string salesPath;
        private string offersPath;
        private string counterOffersPath;
        private string prefsPath;

        // Time tracking
        private DateTime modStartTime;
        private float lastExportTime = 0f;
        private float exportInterval = 300f; // 5 minutes

        // Store drug types for products
        private Dictionary<string, string> productDrugTypes = new Dictionary<string, string>();

        [Obsolete]
        public override void OnApplicationStart()
        {
            Instance = this;
            modStartTime = DateTime.Now;

            try
            {
                // Create data directory
                Directory.CreateDirectory(ModDataPath);

                // Initialize file paths
                salesPath = Path.Combine(ModDataPath, "sales_log_latest.csv");
                offersPath = Path.Combine(ModDataPath, "offers_log_latest.csv");
                counterOffersPath = Path.Combine(ModDataPath, "counter_offers_log_latest.csv");
                prefsPath = Path.Combine(ModDataPath, "customer_preferences_latest.csv");

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

                // Delivery evaluation - this was causing an error, but we'll keep it and handle errors better
                PatchMethod(harmony, customerType, "EvaluateDelivery", nameof(EvaluateDelivery_Postfix));

                // Initialize CSV files with headers
                InitializeCSVFiles();

                MelonLogger.Msg("Business Intelligence Mod initialized!");
                MelonLogger.Msg($"Data will be saved to: {ModDataPath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error during initialization: {ex}");
            }
        }

        private void InitializeCSVFiles()
        {
            try
            {
                // Sales log
                if (!File.Exists(salesPath))
                {
                    File.WriteAllText(salesPath, SALES_HEADER + Environment.NewLine);
                }

                // Offers log
                if (!File.Exists(offersPath))
                {
                    File.WriteAllText(offersPath, OFFERS_HEADER + Environment.NewLine);
                }

                // Counter-offers log
                if (!File.Exists(counterOffersPath))
                {
                    File.WriteAllText(counterOffersPath, COUNTER_OFFERS_HEADER + Environment.NewLine);
                }

                // Customer preferences log
                if (!File.Exists(prefsPath))
                {
                    File.WriteAllText(prefsPath, CUSTOMER_PREFERENCES_HEADER + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initializing CSV files: {ex}");
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

        public override void OnUpdate()
        {
            // Periodic export of data
            lastExportTime += Time.deltaTime;
            if (lastExportTime >= exportInterval)
            {
                ExportData("periodic");
                lastExportTime = 0f;
            }
        }

        public override void OnApplicationQuit()
        {
            ConsolidateCSVFiles();
        }

        private void ConsolidateCSVFiles()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string sessionLength = (DateTime.Now - modStartTime).ToString(@"hh\:mm\:ss");

                // Consolidate sales logs
                ConsolidateFile(salesPath, $"sales_log_{timestamp}_final.csv", SALES_HEADER);

                // Consolidate offers logs
                ConsolidateFile(offersPath, $"offers_log_{timestamp}_final.csv", OFFERS_HEADER);

                // Consolidate counter-offers logs
                ConsolidateFile(counterOffersPath, $"counter_offers_log_{timestamp}_final.csv", COUNTER_OFFERS_HEADER);

                // Consolidate customer preferences logs
                ConsolidateFile(prefsPath, $"customer_preferences_{timestamp}_final.csv", CUSTOMER_PREFERENCES_HEADER);

                // Generate session summary
                string summaryPath = Path.Combine(ModDataPath, $"session_summary_{timestamp}_final.txt");
                using (StreamWriter writer = new StreamWriter(summaryPath))
                {
                    writer.WriteLine($"Session Start: {modStartTime}");
                    writer.WriteLine($"Session Length: {sessionLength}");
                    writer.WriteLine($"Total Sales: {salesLog.Count}");
                    writer.WriteLine($"Total Offers: {offerLog.Count}");
                    writer.WriteLine($"Total Counter-Offers: {counterOfferLog.Count}");
                    writer.WriteLine($"Total Customer Preference Entries: {customerPreferencesLog.Count}");

                    // Calculate total revenue
                    float totalRevenue = 0f;
                    foreach (string sale in salesLog)
                    {
                        string[] parts = sale.Split(',');
                        if (parts.Length > 6)
                        {
                            float price;
                            if (float.TryParse(parts[6], out price))
                            {
                                totalRevenue += price;
                            }
                        }
                    }
                    writer.WriteLine($"Total Revenue: ${totalRevenue:F2}");
                }

                MelonLogger.Msg($"Exported session summary to {summaryPath}");

                // Clean up temporary files
                CleanupTemporaryFiles();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error consolidating CSV files: {ex.Message}");
            }
        }

        private void ConsolidateFile(string tempPath, string finalPath, string header)
        {
            try
            {
                string finalFilePath = Path.Combine(ModDataPath, finalPath);

                // Create the consolidated file with header
                using (StreamWriter writer = new StreamWriter(finalFilePath))
                {
                    writer.WriteLine(header);

                    // Read the temporary file skipping the header
                    if (File.Exists(tempPath))
                    {
                        string[] lines = File.ReadAllLines(tempPath);
                        bool isFirstLine = true;

                        foreach (string line in lines)
                        {
                            if (isFirstLine)
                            {
                                isFirstLine = false; // Skip header
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                writer.WriteLine(line);
                            }
                        }
                    }
                }

                MelonLogger.Msg($"Consolidated data to {finalFilePath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error consolidating file {tempPath}: {ex.Message}");
            }
        }

        private void CleanupTemporaryFiles()
        {
            try
            {
                // Delete the temporary files
                if (File.Exists(salesPath)) File.Delete(salesPath);
                if (File.Exists(offersPath)) File.Delete(offersPath);
                if (File.Exists(counterOffersPath)) File.Delete(counterOffersPath);
                if (File.Exists(prefsPath)) File.Delete(prefsPath);

                // Delete any other periodic exports
                string[] periodics = Directory.GetFiles(ModDataPath, "*_periodic.csv");
                foreach (string file in periodics)
                {
                    File.Delete(file);
                }

                MelonLogger.Msg("Cleaned up temporary files");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cleaning up temporary files: {ex.Message}");
            }
        }

        private void ExportData(string exportType)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

                // Only create periodic exports if requested
                if (exportType == "periodic")
                {
                    // Export sales
                    if (salesLog.Count > 0)
                    {
                        string salesExportPath = Path.Combine(ModDataPath, $"sales_log_{timestamp}_{exportType}.csv");
                        using (StreamWriter writer = new StreamWriter(salesExportPath))
                        {
                            writer.WriteLine(SALES_HEADER);
                            foreach (string line in salesLog)
                            {
                                writer.WriteLine(line);
                            }
                        }
                        MelonLogger.Msg($"Exported {salesLog.Count} sale records");
                    }

                    // Export offers
                    if (offerLog.Count > 0)
                    {
                        string offersExportPath = Path.Combine(ModDataPath, $"offers_log_{timestamp}_{exportType}.csv");
                        using (StreamWriter writer = new StreamWriter(offersExportPath))
                        {
                            writer.WriteLine(OFFERS_HEADER);
                            foreach (string line in offerLog)
                            {
                                writer.WriteLine(line);
                            }
                        }
                        MelonLogger.Msg($"Exported {offerLog.Count} offer records");
                    }

                    // Export counter-offers
                    if (counterOfferLog.Count > 0)
                    {
                        string counterOffersExportPath = Path.Combine(ModDataPath, $"counter_offers_log_{timestamp}_{exportType}.csv");
                        using (StreamWriter writer = new StreamWriter(counterOffersExportPath))
                        {
                            writer.WriteLine(COUNTER_OFFERS_HEADER);
                            foreach (string line in counterOfferLog)
                            {
                                writer.WriteLine(line);
                            }
                        }
                        MelonLogger.Msg($"Exported {counterOfferLog.Count} counter-offer records");
                    }

                    // Export customer preferences
                    if (customerPreferencesLog.Count > 0)
                    {
                        string preferencesExportPath = Path.Combine(ModDataPath, $"customer_preferences_{timestamp}_{exportType}.csv");
                        using (StreamWriter writer = new StreamWriter(preferencesExportPath))
                        {
                            writer.WriteLine(CUSTOMER_PREFERENCES_HEADER);
                            foreach (string line in customerPreferencesLog)
                            {
                                writer.WriteLine(line);
                            }
                        }
                        MelonLogger.Msg($"Exported {customerPreferencesLog.Count} customer preference records");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error exporting data: {ex.Message}");
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

                // Get game time - instead of using Day property
                string gameTime = DateTime.Now.ToString("HH:mm:ss");

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

                // Build CSV record with updated format
                string logEntry = string.Join(",", new string[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    gameTime,
                    customerName,
                    dealerName,
                    handoverByPlayer.ToString(),
                    totalPayment.ToString("F2"),
                    satisfaction.ToString("F4"),
                    totalQuantityRequested.ToString(),
                    totalQuantityProvided.ToString(),
                    itemIDs,
                    itemTypes
                });

                Instance.salesLog.Add(logEntry);
                MelonLogger.Msg($"Tracked sale: {logEntry}");

                // Immediately save for testing
                File.AppendAllText(Instance.salesPath, logEntry + Environment.NewLine);

                // Also track customer preferences after a sale
                if (__instance.CurrentAddiction > 0)
                {
                    string mainDrugType = "Unknown";
                    // If we can get the main drug type from the product list
                    if (productList != null && productList.entries.Count > 0)
                    {
                        mainDrugType = ClassifyProductType(productList.entries[0].ProductID);
                    }

                    string prefsEntry = string.Join(",", new string[]
                    {
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        gameTime,
                        customerName,
                        __instance.CurrentAddiction.ToString("F4"),
                        "0", // HighestAddiction - placeholder
                        mainDrugType,
                        "Sale" // Source of preference data
                    });

                    Instance.customerPreferencesLog.Add(prefsEntry);
                    File.AppendAllText(Instance.prefsPath, prefsEntry + Environment.NewLine);
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

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        itemsDetails += $"{item.ID}({item.Quantity}),";
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

                // Get game time
                string gameTime = DateTime.Now.ToString("HH:mm:ss");

                // Build CSV record
                string logEntry = string.Join(",", new string[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    gameTime,
                    customerName,
                    entry.ProductID,
                    productType,
                    entry.Quantity.ToString(),
                    __instance.OfferedContractInfo.Payment.ToString("F2"),
                    window.ToString(),
                    successChance.ToString("F4"),
                    "True" // Accepted
                });

                Instance.offerLog.Add(logEntry);
                MelonLogger.Msg($"Tracked accepted offer: {logEntry}");

                // Immediately save for testing
                File.AppendAllText(Instance.offersPath, logEntry + Environment.NewLine);
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

                // Get game time
                string gameTime = DateTime.Now.ToString("HH:mm:ss");

                // Build CSV record
                string logEntry = string.Join(",", new string[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    gameTime,
                    customerName,
                    entry.ProductID,
                    productType,
                    entry.Quantity.ToString(),
                    __instance.OfferedContractInfo.Payment.ToString("F2"),
                    "N/A", // No window for rejected offer
                    successChance.ToString("F4"),
                    "False" // Rejected
                });

                Instance.offerLog.Add(logEntry);
                MelonLogger.Msg($"Tracked rejected offer: {logEntry}");

                // Immediately save for testing
                File.AppendAllText(Instance.offersPath, logEntry + Environment.NewLine);
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

                // Get game time
                string gameTime = DateTime.Now.ToString("HH:mm:ss");

                // We can't call EvaluateCounteroffer directly as it's protected
                // Try to infer acceptance based on later behavior
                bool accepted = true; // Assume accepted for now

                // Build CSV record
                string logEntry = string.Join(",", new string[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    gameTime,
                    customerName,
                    originalEntry.ProductID,
                    originalProductType,
                    originalEntry.Quantity.ToString(),
                    __instance.OfferedContractInfo.Payment.ToString("F2"),
                    productID,
                    counterProductType,
                    quantity.ToString(),
                    price.ToString("F2"),
                    accepted.ToString()
                });

                Instance.counterOfferLog.Add(logEntry);
                MelonLogger.Msg($"Tracked counter offer: {logEntry}");

                // Immediately save for testing
                File.AppendAllText(Instance.counterOffersPath, logEntry + Environment.NewLine);
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

                // Get game time
                string gameTime = DateTime.Now.ToString("HH:mm:ss");

                // Store the drug type information for future lookup
                string mainDrugTypeStr = mainDrugType.ToString();

                // Log customer addiction level and preferred drug type
                string prefsEntry = string.Join(",", new string[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    gameTime,
                    customerName,
                    __instance.CurrentAddiction.ToString("F4"),
                    highestAddiction.ToString("F4"),
                    mainDrugTypeStr,
                    "Delivery" // Source of preference data
                });

                Instance.customerPreferencesLog.Add(prefsEntry);

                // Immediately save for testing
                File.AppendAllText(Instance.prefsPath, prefsEntry + Environment.NewLine);

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
        private Dictionary<int, OfferChanceData> offerChanceCache = new Dictionary<int, OfferChanceData>();

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