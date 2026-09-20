namespace VatpacPlugin
{
    /// <summary>
    /// One entry from Servers.json, which is embedded in this assembly and is the only place the server
    /// list lives. These are the servers asked when vatSys connects (see Simulator.Connected), so adding
    /// a sweatbox is a one line change to that file.
    /// </summary>
    public class Server
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
