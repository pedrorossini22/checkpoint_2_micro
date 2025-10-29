using Domain;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Repository;
using Service;
using System.Net;

namespace performance_cache.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private const string cacheKey = "products-cache";
        private readonly IProductRepository productRepository;
        private readonly ICacheService cacheService;
        private readonly ILogger<ProductController> logger;

        public ProductController(IProductRepository productRepository, ICacheService cacheService, ILogger<ProductController> logger)
        {
            this.productRepository = productRepository;
            this.cacheService = cacheService;
            this.logger = logger;
        }

        // GET: api/product
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                logger.LogInformation("Iniciando busca de produtos");

                // buscar do cache Redis
                try
                {
                    await cacheService.SetExpiryAsync(cacheKey, TimeSpan.FromMinutes(20));
                    string? cachedProducts = await cacheService.GetAsync(cacheKey);
                    
                    if (!string.IsNullOrEmpty(cachedProducts))
                    {
                        logger.LogInformation("produtos encontrados no cache Redis");
                        return Ok(cachedProducts);
                    }
                }
                catch (Exception redisEx)
                {
                    logger.LogWarning(redisEx, "Erro ao acessar cache Redis, continuando sem cache");
                }

                // Buscar no banco de dados
                var productList = await productRepository.GetAllProductsAsync();

                if (productList == null || !productList.Any())
                {
                    logger.LogInformation("Nenhum produto encontrado no banco de dados");
                    return Ok(new List<Product>());
                }

                // Tentativa de salvar no cache
                try
                {
                    var productListJson = JsonConvert.SerializeObject(productList);
                    await cacheService.SetAsync(cacheKey, productListJson, TimeSpan.FromMinutes(20));
                    logger.LogInformation("Dados salvos no cache Redis");
                }
                catch (Exception cacheEx)
                {
                    logger.LogWarning(cacheEx, "Erro ao salvar no cache Redis, mas dados foram retornados");
                }

                logger.LogInformation("Retornando {Count} produtos", productList.Count());
                return Ok(productList);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro interno ao buscar produtos no sistema");
                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new { message = "Erro interno do servidor ao buscar produtos no sistema", timestamp = DateTime.UtcNow });
            }
        }

        // POST: api/product
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Product product)
        {
            try
            {
                if (product == null)
                {
                    logger.LogWarning("Tentativa de criar produto com dados nulos");
                    return BadRequest(new { message = "Dados do produto são obrigatórios", timestamp = DateTime.UtcNow });
                }

                // Validação básica dos campos obrigatórios
                if (string.IsNullOrWhiteSpace(product.Nome) ||
                    string.IsNullOrWhiteSpace(product.Categoria) ||
                    string.IsNullOrWhiteSpace(product.Preco))
                {
                    logger.LogWarning("Tentativa de criar produto com campos obrigatórios vazios");
                    return BadRequest(new
                    {
                        message = "nome, categoria e preco são campos obrigatórios",
                        timestamp = DateTime.UtcNow
                    });
                }

                logger.LogInformation("Criando novo produto: {nome} {categoria} - {preco}", product.Nome, product.Categoria, product.preco);

                var newProduct = await productRepository.AddProductAsync(product);

                if (newProduct == null)
                {
                    logger.LogError("erro ao criar produto - repository retornou null");
                    return StatusCode((int)HttpStatusCode.InternalServerError,
                        new { message = "Erro interno ao criar produto", timestamp = DateTime.UtcNow });
                }

                await InvalidateCache();
                logger.LogInformation("produto criado com sucesso - ID: {Id}", newProduct);

                return CreatedAtAction(nameof(Get), new { id = newProduct }, newProduct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro interno ao criar produto");
                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new { message = "Erro interno do servidor ao criar produto", timestamp = DateTime.UtcNow });
            }
        }

        // PUT: api/product/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Product product)
        {
            try
            {
                if (id <= 0)
                {
                    logger.LogWarning("Tentativa de atualizar produto com ID inválido: {Id}", id);
                    return BadRequest(new { message = "ID do produto deve ser maior que zero", timestamp = DateTime.UtcNow });
                }

                if (product == null)
                {
                    logger.LogWarning("Tentativa de atualizar produto com dados nulos para ID: {Id}", id);
                    return BadRequest(new { message = "Dados do produto são obrigatórios", timestamp = DateTime.UtcNow });
                }

                // Validação básica dos campos obrigatórios
                if (string.IsNullOrWhiteSpace(product.Nome) ||
                    string.IsNullOrWhiteSpace(product.Categoria) ||
                    string.IsNullOrWhiteSpace(product.Preco))
                {
                    logger.LogWarning("Tentativa de atualizar produto com campos obrigatórios vazios para ID: {Id}", id);
                    return BadRequest(new
                    {
                        message = "nome, categoria e preço são campos obrigatórios",
                        timestamp = DateTime.UtcNow
                    });
                }

                product.Id = id;
                logger.LogInformation("Atualizando produto ID: {Id} - {Nome} {Categoria} - {Preco}", id, product.Nome, product.Categoria, product.Preco);

                await productRepository.UpdateProductAsync(product);

                await InvalidateCache();
                logger.LogInformation("produto atualizado com sucesso - ID: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro interno ao atualizar produto ID: {Id}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new { message = "Erro interno do servidor ao atualizar produto", timestamp = DateTime.UtcNow });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (id <= 0)
                {
                    logger.LogWarning("Tentativa de excluir produto com ID inválido: {Id}", id);
                    return BadRequest(new { message = "ID do produto deve ser maior que zero", timestamp = DateTime.UtcNow });
                }

                logger.LogInformation("Excluindo produto ID: {Id}", id);

                await productRepository.DeleteProductAsync(id);

                await InvalidateCache();
                logger.LogInformation("produto excluído com sucesso - ID: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro interno ao excluir produto ID: {Id}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new { message = "Erro interno do servidor ao excluir produto", timestamp = DateTime.UtcNow });
            }
        }

        private async Task InvalidateCache()
        {
            try
            {
                await cacheService.DeleteAsync(cacheKey);
                logger.LogInformation("Cache invalidado com sucesso");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erro ao invalidar cache Redis, mas operação continuará");
            }
        }
    }
}
