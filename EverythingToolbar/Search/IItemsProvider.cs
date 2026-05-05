using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace EverythingToolbar.Search
{
    /// <summary>
    /// Defines a contract for paginated or virtualized data fetching.
    /// Provides methods to retrieve the total count and specific ranges of items asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of items provided by this implementation.</typeparam>
    public interface IItemsProvider<T> : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets a value indicating whether the provider is currently busy fetching data.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Fetches the total number of items available for the given page size.
        /// </summary>
        /// <param name="pageSize">The number of items per page.</param>
        /// <param name="isAsync">Determines whether the fetch operation should be performed asynchronously.</param>
        /// <returns>A task that represents the asynchronous fetch operation. The task result contains the total item count.</returns>
        Task<int> FetchCount(int pageSize, bool isAsync);

        /// <summary>
        /// Fetches a specific range of items based on the starting index and page size.
        /// </summary>
        /// <param name="startIndex">The zero-based index of the first item to retrieve.</param>
        /// <param name="pageSize">The maximum number of items to retrieve.</param>
        /// <param name="isAsync">Determines whether the fetch operation should be performed asynchronously.</param>
        /// <returns>A task that represents the asynchronous fetch operation. The task result contains a list of items for the requested range.</returns>
        Task<IList<T>> FetchRange(int startIndex, int pageSize, bool isAsync);
    }
}