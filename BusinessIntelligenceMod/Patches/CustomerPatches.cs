using System.Globalization;
using HarmonyLib;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppFishNet.Object;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Handover;
using BusinessIntelligenceMod.Utils;
using MelonLoader;

namespace BusinessIntelligenceMod.Patches;

[HarmonyPatch]
public static class CustomerPatches
{
    // TODO
    // The cache dictionary should be filled for each customer interaction
    // as it was not in the MONO version
    // in the patch methods, i've added it but not all
    private static readonly Dictionary<int, OfferChanceData> OfferChanceCache = new();

    private const string EventSale = "SALE";
    private const string EventOfferAccepted = "OFFER_ACCEPTED";
    private const string EventOfferRejected = "OFFER_REJECTED";
    private const string EventCounterOffer = "COUNTER_OFFER";
    private const string EventCustomerPreference = "CUSTOMER_PREFERENCE";
    private const string EventOfferChance = "OFFER_CHANCE";

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Customer), "ProcessHandoverServerSide")]
    public static void ProcessHandoverServerSide_Postfix(
        Customer __instance,
        HandoverScreen.EHandoverOutcome outcome,
        List<ItemInstance> items,
        bool handoverByPlayer,
        float totalPayment,
        ProductList productList,
        float satisfaction,
        NetworkObject dealer
    )
    {
        try
        {
            if (outcome != HandoverScreen.EHandoverOutcome.Finalize)
                return;

            MelonLogger.Msg("ProcessHandoverServerSide_Postfix executed!");

            var customerName = __instance?.NPC?.fullName ?? "Unknown";
            var dealerName = handoverByPlayer ? "Player" : dealer?.name ?? "NPC";

            var totalQuantityRequested = 0;
            var totalQuantityProvided = 0;
            var itemIDs = "";
            var itemTypes = "";

            if (productList != null && productList.entries.Count > 0)
            {
                foreach (var entry in productList.entries)
                {
                    totalQuantityRequested += entry.Quantity;
                    var productType = Helpers.ClassifyProductType(entry.ProductID);
                    itemIDs += $"{entry.ProductID}({entry.Quantity});";
                    itemTypes += $"{productType};";
                }
            }

            if (items != null)
            {
                totalQuantityProvided += items.Sum(item => item.Quantity);
            }

            var payload = CsvLogger.CreatePayload(
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

            CsvLogger.LogEvent(EventSale, payload);

            MelonLogger.Msg(
                $"Sale: {customerName} - {dealerName} - Payment: ${totalPayment:F2}, Satisfaction: {satisfaction:F4}");

            // Also track customer preferences after a sale

            if (!(__instance.CurrentAddiction > 0)) return;
            var mainDrugType = "Unknown";
            // If we can get the main drug type from the product list
            if (productList != null && productList.entries.Count > 0)
            {
                mainDrugType = Helpers.ClassifyProductType(productList.entries[0].ProductID);
            }

            var prefsPayload = CsvLogger.CreatePayload(
                new KeyValuePair<string, string>("customer", customerName),
                new KeyValuePair<string, string>("currentAddiction", __instance.CurrentAddiction.ToString("F4")),
                new KeyValuePair<string, string>("highestAddiction", "0"), // Placeholder
                new KeyValuePair<string, string>("mainDrugType", mainDrugType),
                new KeyValuePair<string, string>("source", "Sale")
            );

            // Log the customer preference event
            CsvLogger.LogEvent(EventCustomerPreference, prefsPayload);
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in ProcessHandoverServerSide_Postfix: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Customer), "GetOfferSuccessChance")]
    public static void GetOfferSuccessChance_Postfix(
        Customer __instance,
        List<ItemInstance> items,
        float askingPrice,
        ref float __result)
    {
        try
        {
            var customerID = __instance.GetInstanceID();
            var customerName = __instance.NPC?.fullName ?? "Unknown";

            var itemsDetails = "";
            float totalQuantity = 0;
            var itemTypes = "";

            if (items != null)
            {
                foreach (var item in items)
                {
                    var productType = Helpers.ClassifyProductType(item.ID);
                    itemsDetails += $"{item.ID}({item.Quantity});";
                    itemTypes += $"{productType};";
                    totalQuantity += item.Quantity;
                }
            }

            // If we're offering something with a defined price, keep it for logging
            if (askingPrice > 0 && totalQuantity > 0)
            {
                CacheSuccessChance(customerID, customerName, items, askingPrice, __result);

                var payload = CsvLogger.CreatePayload(
                    new KeyValuePair<string, string>("customer", customerName),
                    new KeyValuePair<string, string>("items", itemsDetails),
                    new KeyValuePair<string, string>("itemTypes", itemTypes),
                    new KeyValuePair<string, string>("askingPrice", askingPrice.ToString("F2")),
                    new KeyValuePair<string, string>("totalQuantity",
                        totalQuantity.ToString(CultureInfo.InvariantCulture)),
                    new KeyValuePair<string, string>("successChance", __result.ToString("F4"))
                );

                // Log the offer chance event
                CsvLogger.LogEvent(EventOfferChance, payload);

                // Log the cached value to verify it's working
                MelonLogger.Msg(
                    $"Cached success chance: {__result:F4} for customer {customerName} (ID: {customerID})");
            }


            MelonLogger.Msg($"Offer Chance: {customerName} - {__result:P1}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in GetOfferSuccessChance_Postfix: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Customer), "PlayerAcceptedContract")]
    public static void PlayerAcceptedContract_Postfix(Customer __instance, EDealWindow window)
    {
        try
        {
            if (__instance.OfferedContractInfo == null ||
                __instance.OfferedContractInfo.Products.entries.Count == 0)
                return;

            var customerName = __instance.NPC?.fullName ?? "Unknown";
            var customerID = __instance.GetInstanceID();

            var entry = __instance.OfferedContractInfo.Products.entries[0];
            string productType = Helpers.ClassifyProductType(entry.ProductID);

            float successChance = -1;
            if (OfferChanceCache.TryGetValue(customerID, out var offer))
            {
                successChance = offer.SuccessChance;
                MelonLogger.Msg(
                    $"Retrieved cached success chance: {successChance:F4} for customer {customerName} (ID: {customerID})");
            }
            else
            {
                successChance = __instance.GetOfferSuccessChance(new List<ItemInstance>
                {
                    new ItemInstance(entry.ProductID, entry.Quantity)
                }, __instance.OfferedContractInfo.Payment);

                CacheSuccessChance(customerID, customerName, new List<ItemInstance>
                {
                    new ItemInstance(entry.ProductID, entry.Quantity)
                }, __instance.OfferedContractInfo.Payment, successChance);

                MelonLogger.Msg(
                    $"Cached new success chance: {successChance:F4} for customer {customerName} (ID: {customerID})");
            }

            string payload = CsvLogger.CreatePayload(
                new KeyValuePair<string, string>("customer", customerName),
                new KeyValuePair<string, string>("productID", entry.ProductID),
                new KeyValuePair<string, string>("productType", productType),
                new KeyValuePair<string, string>("quantity", entry.Quantity.ToString()),
                new KeyValuePair<string, string>("price", __instance.OfferedContractInfo.Payment.ToString("F2")),
                new KeyValuePair<string, string>("window", window.ToString()),
                new KeyValuePair<string, string>("successChance", successChance.ToString("F4"))
            );

            CsvLogger.LogEvent(EventOfferAccepted, payload);
            MelonLogger.Msg(
                $"Offer Accepted: {customerName} - {entry.ProductID} {entry.Quantity} units at ${__instance.OfferedContractInfo.Payment:F2}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in PlayerAcceptedContract_Postfix: {ex.Message}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Customer), "ContractRejected")]
    public static void ContractRejected_Prefix(Customer __instance)
    {
        try
        {
            if (__instance.OfferedContractInfo == null ||
                __instance.OfferedContractInfo.Products.entries.Count == 0)
                return;

            var customerName = __instance.NPC?.fullName ?? "Unknown";
            var customerID = __instance.GetInstanceID();

            var entry = __instance.OfferedContractInfo.Products.entries[0];
            var productType = Helpers.ClassifyProductType(entry.ProductID);

            float successChance = -1;

            if (OfferChanceCache.TryGetValue(customerID, out var offer))
            {
                successChance = offer.SuccessChance;
                MelonLogger.Msg(
                    $"Retrieved cached success chance: {successChance:F4} for customer {customerName} (ID: {customerID})");
            }
            else
            {
                MelonLogger.Warning(
                    $"No cached success chance found for customer {customerName} (ID: {customerID})");
            }

            var payload = CsvLogger.CreatePayload(
                new KeyValuePair<string, string>("customer", customerName),
                new KeyValuePair<string, string>("productID", entry.ProductID),
                new KeyValuePair<string, string>("productType", productType),
                new KeyValuePair<string, string>("quantity", entry.Quantity.ToString()),
                new KeyValuePair<string, string>("price", __instance.OfferedContractInfo.Payment.ToString("F2")),
                new KeyValuePair<string, string>("successChance", successChance.ToString("F4"))
            );
            CsvLogger.LogEvent(EventOfferRejected, payload);
            MelonLogger.Msg(
                $"Offer Rejected: {customerName} - {entry.ProductID} {entry.Quantity} units at ${__instance.OfferedContractInfo.Payment:F2}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in ContractRejected_Postfix: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Customer), "ProcessCounterOfferServerSide")]
    public static void ProcessCounterOfferServerSide_Postfix(
        Customer __instance,
        string productID,
        int quantity,
        float price)
    {
        try
        {
            if (__instance.OfferedContractInfo == null ||
                __instance.OfferedContractInfo.Products.entries.Count == 0)
                return;


            var customerName = __instance.NPC?.fullName ?? "Unknown";

            var originalEntry = __instance.OfferedContractInfo.Products.entries[0];

            var originalProductType = Helpers.ClassifyProductType(originalEntry.ProductID);
            var counterProductType = Helpers.ClassifyProductType(productID);

            var accepted = true; // Assume it as "accepted" for now

            int customerID = __instance.GetInstanceID();

            var itemsForCache = new List<ItemInstance>
            {
                new ItemInstance(productID, quantity)
            };

            float recalculatedChance = __instance.GetOfferSuccessChance(itemsForCache, price);

            OfferChanceCache[customerID] = new OfferChanceData
            {
                Timestamp = DateTime.Now,
                CustomerName = customerName,
                Items = itemsForCache,
                AskingPrice = price,
                SuccessChance = recalculatedChance
            };

            var payload = CsvLogger.CreatePayload(
                new KeyValuePair<string, string>("customer", customerName),
                new KeyValuePair<string, string>("originalProductID", originalEntry.ProductID),
                new KeyValuePair<string, string>("originalProductType", originalProductType),
                new KeyValuePair<string, string>("originalQuantity", originalEntry.Quantity.ToString()),
                new KeyValuePair<string, string>("originalPrice",
                    __instance.OfferedContractInfo.Payment.ToString("F2")),
                new KeyValuePair<string, string>("counterProductID", productID),
                new KeyValuePair<string, string>("counterProductType", counterProductType),
                new KeyValuePair<string, string>("counterQuantity", quantity.ToString()),
                new KeyValuePair<string, string>("counterPrice", price.ToString("F2")),
                new KeyValuePair<string, string>("accepted", accepted.ToString())
            );

            CsvLogger.LogEvent(EventCounterOffer, payload);

            MelonLogger.Msg(
                $"Counter Offer by {customerName} - Original: {originalEntry.ProductID} {originalEntry.Quantity} units at ${__instance.OfferedContractInfo.Payment:F2}, Counter: {productID} {quantity} units at ${price:F2}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in ProcessCounterOfferServerSide_Postfix: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Customer), "EvaluateDelivery")]
    public static void EvaluateDelivery_Postfix(Customer __instance, Contract contract,
        List<ItemInstance> providedItems,
        ref float highestAddiction, ref EDrugType mainTypeType,
        ref int matchedProductCount, float __result)
    {
        try
        {
            var customerName = __instance.NPC?.fullName ?? "Unknown";
            var mainDrugTypeStr = mainTypeType.ToString();

            var payload = CsvLogger.CreatePayload(
                new KeyValuePair<string, string>("customer", customerName),
                new KeyValuePair<string, string>("currentAddiction", __instance.CurrentAddiction.ToString("F4")),
                new KeyValuePair<string, string>("highestAddiction", highestAddiction.ToString("F4")),
                new KeyValuePair<string, string>("mainDrugType", mainDrugTypeStr),
                new KeyValuePair<string, string>("matchedProductCount", matchedProductCount.ToString()),
                new KeyValuePair<string, string>("satisfaction", __result.ToString("F4")),
                new KeyValuePair<string, string>("source", "Delivery")
            );

            CsvLogger.LogEvent(EventCustomerPreference, payload);
            MelonLogger.Msg(
                $"Evaluate Delivery: {customerName} - Current Addiction: {__instance.CurrentAddiction:F4}, Highest Addiction: {highestAddiction:F4}, Main Drug Type: {mainDrugTypeStr}, Matched Product Count: {matchedProductCount}, Satisfaction: {__result:F4}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error in EvaluateDelivery_Postfix: {ex.Message}");
        }
    }

    private static void CacheSuccessChance(int customerID, string customerName, List<ItemInstance> items,
        float askingPrice, float successChance)
    {
        if (!OfferChanceCache.TryGetValue(customerID, out var cachedData))
        {
            OfferChanceCache[customerID] = new OfferChanceData
            {
                Timestamp = DateTime.Now,
                CustomerName = customerName,
                Items = [..items],
                AskingPrice = askingPrice,
                SuccessChance = successChance
            };
        }
        else
        {
            cachedData.Timestamp = DateTime.Now;
            cachedData.CustomerName = customerName;
            cachedData.Items = new List<ItemInstance>(items);
            cachedData.AskingPrice = askingPrice;
            cachedData.SuccessChance = successChance;
        }
    }
}

public class OfferChanceData
{
    public DateTime Timestamp;
    public string CustomerName;
    public List<ItemInstance> Items;
    public float AskingPrice;
    public float SuccessChance;
}