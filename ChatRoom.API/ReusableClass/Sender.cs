namespace ChatRoom.API.ReusableClass
{
    public class Sender
    {
        public Sender()
        {
        }

        public Sender(int UUID, string avatar, string nickname)
        {
            this.UUID = UUID;
            Avatar = avatar;
            Nickname = nickname;
        }

        public int UUID { get; set; }
        public string Avatar { get; set; }
        public string Nickname { get; set; }
    }
}
