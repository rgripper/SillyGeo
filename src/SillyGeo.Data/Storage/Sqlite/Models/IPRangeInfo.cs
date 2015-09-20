namespace SillyGeo.Data.Storage.Sqlite.Models
{
    internal class IPRangeInfo
    {
        public FlatIPAddress Start { get; set; }

        public FlatIPAddress End { get; set; }

        public int CountryId { get; set; }

        public int? PopulatedPlaceId { get; set; }
    }
}
