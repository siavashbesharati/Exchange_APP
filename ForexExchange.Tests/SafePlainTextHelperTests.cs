using ForexExchange.Helpers;

namespace ForexExchange.Tests
{
    public class SafePlainTextHelperTests
    {
        [Theory]
        [InlineData("سند نقدی")]
        [InlineData("Cash - Customer A to Customer B")]
        [InlineData("Amount 1,000 USD")]
        public void IsValid_ShouldAllowSafeTitles(string title)
        {
            Assert.True(SafePlainTextHelper.IsValid(title));
            Assert.Null(SafePlainTextHelper.GetValidationError(title));
        }

        [Theory]
        [InlineData("سند با ' علامت")]
        [InlineData(@"path\\to\\document")]
        [InlineData("line1\nline2")]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("broken <div tag")]
        [InlineData("encoded &lt;tag&gt;")]
        public void IsValid_ShouldRejectUnsafeTitles(string title)
        {
            Assert.False(SafePlainTextHelper.IsValid(title));
            Assert.Equal(SafePlainTextHelper.ValidationErrorMessage, SafePlainTextHelper.GetValidationError(title));
        }

        [Fact]
        public void Sanitize_ShouldRemoveUnsafeCharacters()
        {
            var sanitized = SafePlainTextHelper.Sanitize("Cash 'test' \\ path <tag>");

            Assert.Equal("Cash test  path tag", sanitized);
        }
    }
}
