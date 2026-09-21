namespace LB_Common.Forms
{
    public class Item
    {
        public int ID { get; private set; }
        public string Name { get; private set; }

        public Item(int id, string name)
        {
            ID = id;
            Name = name;
        }
    }
}
