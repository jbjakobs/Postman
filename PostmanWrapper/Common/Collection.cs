namespace Postman.Common
{
    public partial class Collection
    {
        public Info info { get; set; }
        public Item[] item { get; set; }
    }

    public partial class Info
    {
        public string name { get; set; }
    }

    public partial class Item
    {
        public string name { get; set; }
        public string description { get; set; }
    }
}
