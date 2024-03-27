namespace Library.Commons.Game.Domain.Entities.Configurations
{
    public class SnakeConfiguration
    {
        public const string SectionName = "SnakeConfiguration";
        public double Speed{get;set;}
        public int HeadSize{get;set;}
        public int InitialSnakeSize { get; set; }
    }
}
