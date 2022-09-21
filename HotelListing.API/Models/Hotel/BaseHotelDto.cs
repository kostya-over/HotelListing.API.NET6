using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
namespace HotelListing.API.Models.Hotel;

public abstract class BaseHotelDto
{
    [Required]
    public string Name { get; set; }
    [Required]
    public string Address { get; set; }
    
    public double? Rating { get; set; }
    [Required]
    [Range(1, Int32.MaxValue)]
    public  int CountryId { get; set; }
}