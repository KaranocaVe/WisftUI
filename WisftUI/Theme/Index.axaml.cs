using System;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace WisftUI.Theme
{
    public class WisftTheme: Styles
    {
        public WisftTheme()
        {
            try
            {
                var theme = new Styles();
                theme.Add(new StyleInclude(new Uri("avares://WisftUI"))
                {
                    Source = new Uri("avares://WisftUI/Theme/Index.axaml")
                });

                this.Add(theme);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading WisftTheme: {ex.Message}");
            }
        }
    }
}
