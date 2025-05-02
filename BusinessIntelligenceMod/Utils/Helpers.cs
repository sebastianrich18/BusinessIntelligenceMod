using Il2CppScheduleOne.ItemFramework;
using Object = UnityEngine.Object;


namespace BusinessIntelligenceMod.Utils;

public abstract class Helpers
{
    public class Result<T, TError>
    {
        public T Data { get; }
        public TError Error { get; }
        public bool IsSuccess => Error == null;

        private Result(T data, TError error)
        {
            Data = data;
            Error = error;
        }

        public static Result<T, TError> Success(T data) => new(data, default);
        public static Result<T, TError> Failure(TError error) => new(default, error);
    }

    public static class TryCatch
    {
        public static async Task<Result<T, TError>> TryAsync<T, TError>(
            Func<Task<T>> promiseFunc,
            Func<Exception, TError> errorMapper = null) where TError : class
        {
            try
            {
                var data = await promiseFunc();
                return Result<T, TError>.Success(data);
            }
            catch (Exception ex)
            {
                var error = errorMapper != null ? errorMapper(ex) : ex as TError;
                return Result<T, TError>.Failure(error);
            }
        }
    }

    // TODO
    // Add a method to classify product types based on item definitions based on game product IDs
    public static string ClassifyProductType(string productID)
    {
        // Fetch the item definition using the productID
        var itemDefinition = Object.FindObjectsOfType<ItemDefinition>()
            .FirstOrDefault(item => item.ID == productID);
        if (itemDefinition == null)
        {
            return "unknown"; // Return "unknown" if the item definition is not found
        }

        // Classify the product type based on the item definition's properties
        if (itemDefinition.Name.Contains("weed", StringComparison.OrdinalIgnoreCase) ||
            itemDefinition.Tags.Contains("weed"))
        {
            return "weed";
        }

        if (itemDefinition.Name.Contains("meth", StringComparison.OrdinalIgnoreCase) ||
            itemDefinition.Tags.Contains("meth"))
        {
            return "meth";
        }

        if (itemDefinition.Name.Contains("cocaine", StringComparison.OrdinalIgnoreCase) ||
            itemDefinition.Tags.Contains("cocaine"))
        {
            return "cocaine";
        }

        return "unknown"; // Default to "unknown" if no match is found
    }
}