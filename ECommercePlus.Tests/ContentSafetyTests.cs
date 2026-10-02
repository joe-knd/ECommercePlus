using ECommercePlus.Domain;

namespace ECommercePlus.Tests;

public class ContentSafetyTests
{
    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("</title><svg/onload=alert(1)>")]
    [InlineData("<!-- comment -->")]
    [InlineData("javascript:alert(1)")]
    [InlineData("click onclick=steal()")]
    [InlineData("&#60;script&#62;")]
    [InlineData("%3Cscript%3E")]
    [InlineData("Robert'); DROP TABLE products;--")]
    [InlineData("' OR '1'='1")]
    [InlineData("1' or 1=1 --")]
    [InlineData("x UNION SELECT password FROM users")]
    [InlineData("a; DELETE FROM Products")]
    [InlineData("name /* hidden */")]
    [InlineData("EXEC(xp_cmdshell 'dir')")]
    [InlineData("bell\u0007char")]
    public void Unsafe_values_are_detected(string value) =>
        Assert.NotNull(ContentSafety.Check(value));

    [Theory]
    [InlineData("Wireless Mouse")]
    [InlineData("Kids' toy for 3-5 yrs")]
    [InlineData("27\" monitor; IPS panel")]
    [InlineData("Men's & women's running shoes — size 10")]
    [InlineData("R&D edition, 100% cotton")]
    [InlineData("Price < 5 USD, weight > 1kg")]
    [InlineData("Select fabric from our range")]
    [InlineData("Update your setup with this desk")]
    [InlineData("Onion = tasty")]
    [InlineData("Comma, In Product Name")]
    [InlineData("Quote \"Inside\" Name")]
    [InlineData("WeatherBrand™ jacket")]
    [InlineData("")]
    [InlineData(null)]
    public void Normal_product_text_is_allowed(string? value) =>
        Assert.Null(ContentSafety.Check(value));
}
