using HotelListing.API.Models.Hotel;

namespace HotelListing.API.Models.Country;

public class CountryDto : BaseCountryDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string ShortName { get; set; }
    
    public List<HotelDto> Hotels { get; set; }
}