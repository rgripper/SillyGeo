namespace SillyGeo.Infrastructure
{
    public class PopulatedPlace : Area
    {
        public int CountryId { get; set; }

        public int? AdminAreaLevel1Id { get; set; }

        public int? AdminAreaLevel2Id { get; set; }

        public Coordinates Coordinates { get; set; }
    }
}
