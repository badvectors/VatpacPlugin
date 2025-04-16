namespace VatpacPlugin
{
    public class Login
    {
        public Login() { }

        public Login(int cid, string password) 
        { 
            CID = cid;
            Password = password;
        }

        public int CID { get; set; }    
        public string Password { get; set; }
    }
}