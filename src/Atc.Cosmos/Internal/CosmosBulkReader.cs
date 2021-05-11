using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Atc.Cosmos.Internal
{
    public class CosmosBulkReader<T> : ICosmosBulkReader<T>
        where T : class, ICosmosResource
    {
        private const string ReadAllQuery = "SELECT * FROM c";
        private readonly Container container;

        public CosmosBulkReader(ICosmosContainerProvider containerProvider)
        {
            this.container = containerProvider.GetContainer<T>(allowBulk: true);
        }

        public async Task<T> ReadAsync(
            string documentId,
            string partitionKey,
            CancellationToken cancellationToken = default)
        {
            var result = await container
                .ReadItemAsync<T>(
                    documentId,
                    new PartitionKey(partitionKey),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            result.Resource.ETag = result.ETag;

            return result.Resource;
        }

        public async Task<T?> FindAsync(
            string documentId,
            string partitionKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ReadAsync(
                    documentId,
                    partitionKey,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (CosmosException)
            {
                return default;
            }
        }

        public async IAsyncEnumerable<T> ReadAllAsync(
            string partitionKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var reader = container.GetItemQueryIterator<T>(
                ReadAllQuery,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey),
                });

            while (reader.HasMoreResults && !cancellationToken.IsCancellationRequested)
            {
                var documents = await reader
                    .ReadNextAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (var document in documents)
                {
                    yield return document;
                }
            }
        }

        public async IAsyncEnumerable<T> QueryAsync(
            QueryDefinition query,
            string partitionKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var reader = container.GetItemQueryIterator<T>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey),
                });

            while (reader.HasMoreResults && !cancellationToken.IsCancellationRequested)
            {
                var documents = await reader
                    .ReadNextAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (var document in documents)
                {
                    yield return document;
                }
            }
        }
    }
}