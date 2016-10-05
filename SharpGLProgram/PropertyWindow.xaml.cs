using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;
using GlmNet; 

namespace SharpGLProgram
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PropertyWindow : Window
    {
        public List<Brush> ColorsList { get; set; }
        static public double LCutOffValue=0, HCutOffValue=255;
        static public int NumColorLevels = 2;
        public string   LowRColor, LowGColor, LowBColor, HighRColor, HighGColor, HighBColor,   // format: #xxxxxx
                        PreColorPalette;   // selected pre-defined color scheme
        static public bool LinearMode = true;
        static public int curPrecolorIndex = 1, curPrecolorColorLevels = 2, curUserColorLevels = 2;
        static public string curUserLowColor = "Yellow", curUserHighColor = "Black";

        public event Action<string> applyOkCancel;

        public PropertyWindow()
        {
            InitializeComponent();

            if (LinearMode)
                LinearLog.SelectedValue = "Linear";
            else
                LinearLog.SelectedValue = "Logarithmic";

            LCutOff.Text = LCutOffValue.ToString();
            HCutOff.Text = HCutOffValue.ToString();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load("ColorPalette.xml");

            XmlNodeList colorList = xmlDoc.SelectNodes("/palettes/palette/@name");
            foreach (XmlNode name in colorList)
            {
                ColourTest.Items.Add(name.InnerText);
            }
            //ColourTest.SelectedValuePath = "@name";
            ColourTest.SelectedIndex = curPrecolorIndex - 1;  // warning: ColourTest.SelectedIndex starts from 0, while the xmlDoc.SelectSingleNode refers to the index from 1

            PreColor.IsChecked = !MainWindow.userColorSelection;
            int defaultIndex = curPrecolorIndex;
            XmlNode defaultLevels = xmlDoc.SelectSingleNode("/palettes/palette["+defaultIndex+"]/@levels");

            UserColor.IsChecked = MainWindow.userColorSelection;
            LowColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(curUserLowColor);
            HighColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(curUserHighColor);

            if ((bool)PreColor.IsChecked && defaultLevels != null)
                ColorLevel.Text = defaultLevels.InnerText;
            else if ((bool)UserColor.IsChecked)
                ColorLevel.Text = MainWindow.userColorLevels.ToString();

            loadSelectedColorPalette();
        }

        public void loadSelectedColorPalette()
        {
            if (stackPanel1.Children.Count != 0)
            {
                stackPanel1.Children.Clear();
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load("ColorPalette.xml");

            curPrecolorIndex = ColourTest.SelectedIndex + 1;
            string xpath = "/palettes/palette[" + curPrecolorIndex + "]/colorItem/@color";
            XmlNodeList locationNode = xmlDoc.SelectNodes(xpath);
            if (locationNode != null)
            {
                MainWindow.rgbValues.Clear();
                for (int i = 0; i < locationNode.Count; i++)
                {
                    MainWindow.rgbValues.Add(locationNode[i].InnerText);
                }
            }

            string xpath1 = "/palettes/palette[" + curPrecolorIndex + "]/@levels";
            XmlNode locationNode1 = xmlDoc.SelectSingleNode(xpath1);
            if ((bool)PreColor.IsChecked && locationNode1 != null)
                    ColorLevel.Text = locationNode1.InnerText;
            else
                    ColorLevel.Text = NumColorLevels.ToString();
            
            curPrecolorColorLevels = int.Parse(ColorLevel.Text);
            for (int i = 0; i < locationNode.Count; i++)
            {
                System.Windows.Controls.TextBlock newTB = new TextBlock();
                newTB.Width = stackPanel1.ActualWidth / locationNode.Count;
                newTB.Background = Brushes.Red;
                string[] values = MainWindow.rgbValues[i].Split(',');

                Brush brush = new SolidColorBrush(Color.FromRgb(Convert.ToByte(values[0]), Convert.ToByte(values[1]), Convert.ToByte(values[2])));
                newTB.Background = brush;

                stackPanel1.Children.Add(newTB);
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            loadSelectedColorPalette();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            Choices();  // read in all form inputs

            applyOkCancel("Apply");
            // Transfer the color info to MainWindow color palette
            //GetColorProperties(out MainWindow.colorPalette);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
           
            Choices();  // read in all form inputs

            applyOkCancel("OK");
            // Transfer the color info to core program
            //GetColorProperties(out MainWindow.colorPalette);
            this.Close();

        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
           this.Close();
        }

        private void UserColor_Checked(object sender, RoutedEventArgs e)
        {
            LowColorPicker.IsEnabled = true;
            HighColorPicker.IsEnabled = true;
            ColorLevel.IsEnabled = true;
            ColorLevel.Text = curUserColorLevels.ToString();
        }

        private void UserColor_Unchecked(object sender, RoutedEventArgs e)
        {
            LowColorPicker.IsEnabled = false;
            HighColorPicker.IsEnabled = false;
            ColorLevel.IsEnabled = false;
        }

        private void PreColor_Checked(object sender, RoutedEventArgs e)
        {
            ColourTest.IsEnabled = true;
            ColorLevel.IsEnabled = false;
            ColorLevel.Text = curPrecolorColorLevels.ToString();
            loadSelectedColorPalette();
        }

        private void PreColor_Unchecked(object sender, RoutedEventArgs e)
        {
            ColourTest.IsEnabled = false;
        }
        
        public void Choices()
        {
            LinearMode = (String.Compare(LinearLog.SelectionBoxItem.ToString(), "Linear", true) == 0) ? true : false;
            LCutOffValue = double.Parse(LCutOff.Text.ToString());
            HCutOffValue = double.Parse(HCutOff.Text.ToString());

            MainWindow.linearColor = LinearMode;
            MainWindow.lowCO = (int) LCutOffValue;
            MainWindow.highCO =(int) HCutOffValue; 
 


            NumColorLevels = int.Parse(ColorLevel.Text);
            //System.Windows.MessageBox.Show("NumColorLevels = " + NumColorLevels.ToString());

            // the following checks are already done on UI level - it's more for sanity check
            if (NumColorLevels < 2) NumColorLevels = 2; // min is 2.. 
            if (NumColorLevels > 255) NumColorLevels = 256; // max is 256 levels 




            
             if (UserColor.IsChecked == true)
             {

                 MainWindow.userColorSelection = true;
                 MainWindow.userColorLevels = NumColorLevels;
                 curUserColorLevels = NumColorLevels;
                 curUserLowColor = LowColorPicker.SelectedColor.ToString();
                 curUserHighColor = HighColorPicker.SelectedColor.ToString();
                                          
                 MainWindow.rgbValues.Clear();
                 LowRColor = LowColorPicker.SelectedColor.Value.R.ToString();
                 LowGColor = LowColorPicker.SelectedColor.Value.G.ToString();
                 LowBColor = LowColorPicker.SelectedColor.Value.B.ToString();
                 HighRColor = HighColorPicker.SelectedColor.Value.R.ToString();
                 HighGColor = HighColorPicker.SelectedColor.Value.G.ToString();
                 HighBColor = HighColorPicker.SelectedColor.Value.B.ToString();
                 
                 MainWindow.rgbValues.Add(LowRColor+", "+LowGColor+", "+LowBColor);
                 MainWindow.rgbValues.Add(HighRColor+", "+HighGColor+", "+HighBColor);
                
             }
             if (PreColor.IsChecked == true)
             {
                 MainWindow.userColorSelection = false;
                 PreColorPalette = ColourTest.SelectedValue.ToString();
                 curPrecolorColorLevels = NumColorLevels;
             }
        }

        public void GetColorProperties(out int[,] colorPalette)
        {

            
            if (NumColorLevels <= 0)
                NumColorLevels = 256;    // default
            colorPalette = new int[NumColorLevels,3];
            
            

            
            double binSize = (double) NumColorLevels / MainWindow.rgbValues.Count();
            int binNo;
            double binLoc, t;

            if (LinearMode)
            {
                for (int i = 0; i < NumColorLevels; i++)
                {
                    binLoc = Convert.ToDouble(i) / binSize;
                    binNo = (int)Math.Floor(binLoc);
                    t = binLoc - binNo;
                    string [] color1val = MainWindow.rgbValues[binNo].Split(',');
                    string [] color2val = MainWindow.rgbValues[binNo+1].Split(',');

                    colorPalette[i, 1] = (int)(t * int.Parse(color1val[0]) + (1 - t) * int.Parse(color2val[0]));
                    colorPalette[i, 2] = (int)(t * int.Parse(color1val[1]) + (1 - t) * int.Parse(color2val[1]));
                    colorPalette[i, 3] = (int)(t * int.Parse(color1val[2]) + (1 - t) * int.Parse(color2val[2]));
                }
            }
            else
            {

            }

           

        }

        // only allow real number input
        private void LCutOff_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // only allow real number input
        private void HCutOff_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // prevent LCutOff value to be equal or higher than current HCutOff value
        private void LCutOff_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            TextBox tbox = sender as TextBox;
            if (double.Parse(tbox.Text) >= double.Parse(HCutOff.Text.ToString()))
            {
                e.Handled = true;
            }
        }

        // prevent HCutOff value to be equal or lower than current LCutOff value
        private void HCutOff_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            TextBox tbox = sender as TextBox;
            if (double.Parse(tbox.Text) <= double.Parse(LCutOff.Text.ToString()))
            {
                e.Handled = true;
            }
        }

        private void ColourTest_Loaded(object sender, RoutedEventArgs e)
        {
            loadSelectedColorPalette();
        }

        private void ColorLevel_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            TextBox tbox = sender as TextBox;
            if (int.Parse(tbox.Text) < 2 || int.Parse(tbox.Text) > 256)
            {
                e.Handled = true;
            }
        }

    }
}
