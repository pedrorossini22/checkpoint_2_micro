using Dapper;
using Domain;
using MySqlConnector;

namespace Repository
{
    public class ProductRepository : Product
    {
        private readonly MySqlConnection _connection;
        public ProductRepository(string connectionString)
        {
            _connection = new MySqlConnection(connectionString);
        }

        public async Task<IEnumerable<Product>> GetAllProductAsync()
        {
            await _connection.OpenAsync();
            string sql = "SELECT id, nome, categoria, preco, quantidade, data_criacao;";
            var product = await _connection.QueryAsync<Product>(sql);
            await _connection.CloseAsync();
            return product;
        }

        public async Task<int> Addsync(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product), "produto invalido.");
            await _connection.OpenAsync();
            string sql = @"
                INSERT INTO PRODUCT (id, nome, categoria, preco, quantidade,data_criacao)
                VALUES (@nome, @categoria, @preco, @quantidade, @data_criacao);
                SELECT LAST_INSERT_ID();
            ";
            var id = await _connection.ExecuteScalarAsync<int>(sql, product);
            await _connection.CloseAsync();
            return id;
        }

        public async Task UpdateVehicleAsync(Product product)
        {
            if (product == null || product.Id <= 0)
                throw new ArgumentException("produto invalido.", nameof(product));
            await _connection.OpenAsync();
            string sql = @"
                UPDATE product
                SET nome = @nome, categoria = @categoria, preco = @preco, quantidade = @Quantidade, data_criacao = @data_criacao
                WHERE id = @Id;
            ";
            await _connection.ExecuteAsync(sql, product);
            await _connection.CloseAsync();
        }

        public async Task DeleteProductAsync(int id)
        {
            if (id <= 0)
                throw new ArgumentException("ID inválido.", nameof(id));
            await _connection.OpenAsync();
            string sql = "DELETE FROM product WHERE id = @Id;";
            await _connection.ExecuteAsync(sql, new { Id = id });
            await _connection.CloseAsync();
        }
    }
}
