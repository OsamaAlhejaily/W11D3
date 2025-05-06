using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Product.Services
{
    public class ProductSorterHostedService : IHostedService
    {
        private readonly ILogger<ProductSorterHostedService> _logger;

        public ProductSorterHostedService(ILogger<ProductSorterHostedService> logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProductSorterHostedService is starting.");

            try
            {
                await SortProductsAsync(cancellationToken);
                _logger.LogInformation("Product files sorting completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sorting product files.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProductSorterHostedService is stopping.");
            return Task.CompletedTask;
        }

        private async Task SortProductsAsync(CancellationToken cancellationToken)
        {
            // 1. Set the File Paths
            string sourceFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "products.txt");
            string targetDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data");

            // 2. Check if Source File Exists
            if (!File.Exists(sourceFilePath))
            {
                _logger.LogError($"Source file not found: {sourceFilePath}");
                return;
            }

            // 3. Ensure Target Directory Exists
            Directory.CreateDirectory(targetDirectory);

            // 4. Set a Batch Size
            const int batchSize = 1000;

            // 5. Open Three Output Files
            string idSortedFilePath = Path.Combine(targetDirectory, "products_sorted_by_id.txt");
            string nameSortedFilePath = Path.Combine(targetDirectory, "products_sorted_by_name.txt");
            string priceSortedFilePath = Path.Combine(targetDirectory, "products_sorted_by_price.txt");

            // Prepare StreamWriters for the sorted files
            using var idSortedFile = new StreamWriter(idSortedFilePath, false);
            using var nameSortedFile = new StreamWriter(nameSortedFilePath, false);
            using var priceSortedFile = new StreamWriter(priceSortedFilePath, false);

            // 6. Read the Source File Line by Line
            var batch = new List<Product>();
            var lineCount = 0;

            // Read all lines from the source file
            var lines = await File.ReadAllLinesAsync(sourceFilePath, cancellationToken);

            foreach (var line in lines)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 3)
                {
                    _logger.LogWarning($"Invalid line format at line {lineCount + 1}: {line}");
                    continue;
                }

                if (int.TryParse(parts[0], out int id) &&
                    decimal.TryParse(parts[2], out decimal price))
                {
                    batch.Add(new Product
                    {
                        Id = id,
                        Name = parts[1],
                        Price = price
                    });
                }
                else
                {
                    _logger.LogWarning($"Invalid product data at line {lineCount + 1}: {line}");
                }

                lineCount++;

                // 7. Write the Batch When Full
                if (batch.Count >= batchSize)
                {
                    await WriteSortedBatchAsync(batch, idSortedFile, nameSortedFile, priceSortedFile, cancellationToken);
                    batch.Clear();
                }
            }

            // 8. Write Any Remaining Records
            if (batch.Count > 0)
            {
                await WriteSortedBatchAsync(batch, idSortedFile, nameSortedFile, priceSortedFile, cancellationToken);
            }

            // 9. Close All File Streams - handled by using statements

            // 10. Display Success Message
            _logger.LogInformation($"Successfully generated sorted product files with {lineCount} valid products.");
        }

        private async Task WriteSortedBatchAsync(
            List<Product> batch,
            StreamWriter idSortedFile,
            StreamWriter nameSortedFile,
            StreamWriter priceSortedFile,
            CancellationToken cancellationToken)
        {
            // Sort by Id
            var sortedById = batch.OrderBy(p => p.Id).ToList();
            foreach (var product in sortedById)
            {
                await idSortedFile.WriteLineAsync($"{product.Id},{product.Name},{product.Price}");
            }
            await idSortedFile.FlushAsync();

            // Sort by Name
            var sortedByName = batch.OrderBy(p => p.Name).ToList();
            foreach (var product in sortedByName)
            {
                await nameSortedFile.WriteLineAsync($"{product.Id},{product.Name},{product.Price}");
            }
            await nameSortedFile.FlushAsync();

            // Sort by Price
            var sortedByPrice = batch.OrderBy(p => p.Price).ToList();
            foreach (var product in sortedByPrice)
            {
                await priceSortedFile.WriteLineAsync($"{product.Id},{product.Name},{product.Price}");
            }
            await priceSortedFile.FlushAsync();
        }
    }

    
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }
}