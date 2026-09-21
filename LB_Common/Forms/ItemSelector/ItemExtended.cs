namespace LB_Common.Forms
{
    public class ItemExtended : Item
    {
        public string PreNameString { get; private set; }
        public string PostNameString { get; private set; }

        public ItemExtended(int id, string name, string preNameStr, string postNameStr) : base(id, name)
        {
            PreNameString = preNameStr;
            PostNameString = postNameStr;
        }
    }
}
