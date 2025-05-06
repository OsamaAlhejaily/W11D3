using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Project.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IConfiguration configuration, ILogger<ProductsController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "id")
        {
            // 2. Validate the Input
            if (pageNumber < 1 || pageSize < 1)
            {
                return BadRequest("Page number and size must be greater than zero.");
            }

            // 3. Determine the Sorted File
            string fileName;
            switch (sortBy.ToLower())
            {
                case "name":
                    fileName = "products_sorted_by_name.txt";
                    break;
                case "price":
                    fileName = "products_sorted_by_price.txt";
                    break;
                default:
                    fileName = "products_sorted_by_id.txt";
                    break;
            }

            // 4. Build the File Path
            string dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            string filePath = Path.Combine(dataDirectory, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogError($"Sorted file not found: {filePath}");
                return NotFound("Sorted file not found.");
            }

            // 5. Calculate the Skip Index
            int skip = (pageNumber - 1) * pageSize;

            try
            {
                // 6. Stream the File and Get the Relevant Products
                var products = new List<Product>();
                var currentIndex = 0;

                foreach (var line in System.IO.File.ReadLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length < 3)
                        continue;

                    // Skip products before the current page
                    if (currentIndex < skip)
                    {
                        currentIndex++;
                        continue;
                    }

                    // Add products for the current page only
                    if (products.Count < pageSize)
                    {
                        if (int.TryParse(parts[0], out int id) &&
                            decimal.TryParse(parts[2], out decimal price))
                        {
                            products.Add(new Product
                            {
                                Id = id,
                                Name = parts[1],
                                Price = price
                            });
                        }
                    }
                    else
                    {
                        // We've reached our pageSize limit, so we can stop reading
                        break;
                    }

                    currentIndex++;
                }

                // 7. Return the Products
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading products file: {ex.Message}");
                return StatusCode(500, "An error occurred while retrieving products.");
            }
        }
    }

    
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }
}