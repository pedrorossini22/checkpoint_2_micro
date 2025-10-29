namespace Domain
{
    public class Product
    {
        public object?[] preco;

        public int Id { get; set; }
        public string Nome { get; set; }
        public string Categoria { get; set; }
        public double { get; set; }
        public int quantidade { get; set; }
        public DateOnly data_criacao { get; set; }
        public string? Preco { get; set; }
    }
}
