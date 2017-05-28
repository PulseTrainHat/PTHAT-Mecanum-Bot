using System;
using System.Threading.Tasks;
using Windows.Gaming.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PulseTrainHATMecanumBotStreamSocket
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Gamepad _Gamepad = null;
        
        //Image set variables
        private BitmapImage JrD = new BitmapImage(new Uri("ms-appx:///Assets/Right_D.png"));
        private BitmapImage JrU = new BitmapImage(new Uri("ms-appx:///Assets/Right.png"));

        private BitmapImage JlD = new BitmapImage(new Uri("ms-appx:///Assets/Left_D.png"));
        private BitmapImage JlU = new BitmapImage(new Uri("ms-appx:///Assets/Left.png"));

        private BitmapImage JfD = new BitmapImage(new Uri("ms-appx:///Assets/Up_D.png"));
        private BitmapImage JfU = new BitmapImage(new Uri("ms-appx:///Assets/Up.png"));

        private BitmapImage JbD = new BitmapImage(new Uri("ms-appx:///Assets/Down_D.png"));
        private BitmapImage JbU = new BitmapImage(new Uri("ms-appx:///Assets/Down.png"));

        private BitmapImage JtrD = new BitmapImage(new Uri("ms-appx:///Assets/trD.png"));
        private BitmapImage JtrU = new BitmapImage(new Uri("ms-appx:///Assets/trU.png"));

        private BitmapImage JtlD = new BitmapImage(new Uri("ms-appx:///Assets/tlD.png"));
        private BitmapImage JtlU = new BitmapImage(new Uri("ms-appx:///Assets/tlU.png"));

        private BitmapImage JbrD = new BitmapImage(new Uri("ms-appx:///Assets/brD.png"));
        private BitmapImage JbrU = new BitmapImage(new Uri("ms-appx:///Assets/brU.png"));

        private BitmapImage JblD = new BitmapImage(new Uri("ms-appx:///Assets/blD.png"));
        private BitmapImage JblU = new BitmapImage(new Uri("ms-appx:///Assets/blU.png"));

        private BitmapImage JccwD = new BitmapImage(new Uri("ms-appx:///Assets/CCWD.png"));
        private BitmapImage JccwU = new BitmapImage(new Uri("ms-appx:///Assets/CCWU.png"));

        private BitmapImage JcwD = new BitmapImage(new Uri("ms-appx:///Assets/CWD.png"));
        private BitmapImage JcwU = new BitmapImage(new Uri("ms-appx:///Assets/CWU.png"));

        private int enable = 1;

        private SocketClient _socket;
        public MainPage()
        {
            this.InitializeComponent();
            txtIp.Text = "192.168.1.136";
            txtPort.Text = "9000";
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_socket != null)
            {
                _socket.Close();
                _socket.OnDataRecived -= socket_OnDataRecived;
                _socket = null;
            }
            _socket = new SocketClient(txtIp.Text, Convert.ToInt32(txtPort.Text));
            _socket.Connect();
            _socket.OnDataRecived += socket_OnDataRecived;
        }

        private async void socket_OnDataRecived(string data)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {  
                //Update Speed TextBox
                km.Text = data;               
            });
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            //Send Data across Network
            _socket.Send(txtMessage.Text);
        }

        private async void Connect_Controller_Click(object sender, RoutedEventArgs e)
        {
            Gamepad.GamepadAdded += Gamepad_GamepadAdded;
            Gamepad.GamepadRemoved += Gamepad_GamepadRemoved;

            while (true)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                           {
                               
                               if (_Gamepad == null)
                               {
                                   return;
                               }

                               // Get the current state
                               var reading = _Gamepad.GetCurrentReading();

                               //Store read values
                               double LTX = reading.LeftThumbstickX;
                               double LTY = reading.LeftThumbstickY;
                               double RTX = reading.RightThumbstickX;
                               double RTY = reading.RightThumbstickY;

                               //---------Rotate Clockwise
                               //Check if gamepad button "B" has been pressed
                               if (reading.Buttons == GamepadButtons.B)
                               {
                                   //Check if Button image source is up
                                   if (Jzd.Source == JcwU)
                                   {
                                       //Sets image source to down
                                       Jzd.Source = JcwD;

                                       //Inputs string in sendbox for visual purposes
                                       txtMessage.Text = "cw";

                                       //Sends data
                                       _socket.Send(txtMessage.Text);
                                   }
                               }
                               else
                               {
                                   //Check if Button image source is not up
                                   if (Jzd.Source != JcwU)
                                   {
                                       //Sets image source to up
                                       Jzd.Source = JcwU;

                                       txtMessage.Text = "cwREL";
                                       _socket.Send(txtMessage.Text);
                                   }
                               }

                               //---------Rotate Counter Clockwise
                               if (reading.Buttons == GamepadButtons.X)
                               {
                                   if (Jzu.Source == JccwU)
                                   {
                                       Jzu.Source = JccwD;
                                       txtMessage.Text = "ccw";
                                       _socket.Send(txtMessage.Text);
                                   }
                               }
                               else
                               {
                                   if (Jzu.Source != JccwU)
                                   {
                                       Jzu.Source = JccwU;
                                       txtMessage.Text = "ccwREL";
                                       _socket.Send(txtMessage.Text);
                                   }
                               }

                               //----------Right
                               //Checks if Thumbsticks are within value range
                               if (LTX <= 1 && LTX > 0.95 && LTY <= 0.55 && LTY >= -0.55)
                               {
                                   if (Jxr.Source == JrU)
                                   {
                                       Jxr.Source = JrD;
                                       txtMessage.Text = "right";
                                       btnSend_Click(null, null);
                                   }
                               }
                               else
                               {
                                   if (Jxr.Source != JrU)
                                   {
                                       Jxr.Source = JrU;
                                       txtMessage.Text = "rightREL";
                                       btnSend_Click(null, null);
                                   }
                               }

                               //----------Left
                               if (LTX >= -1 && LTX < -0.95 && LTY <= 0.55 && LTY >= -0.55)
                               {
                                   if (Jxl.Source == JlU)
                                   {
                                       Jxl.Source = JlD;
                                       txtMessage.Text = "left";
                                       btnSend_Click(null, null);
                                   }
                               }
                               else
                               {
                                   if (Jxl.Source != JlU)
                                   {
                                       Jxl.Source = JlU;
                                       txtMessage.Text = "leftREL";
                                       btnSend_Click(null, null);
                                   }
                               }

                               //----------Forward
                               if (LTY <= 1 && LTY > 0.95 && LTX <= 0.55 && LTX >= -0.55)
                               {
                                   if (Jyf.Source == JfU)
                                   {
                                       Jyf.Source = JfD;
                                       txtMessage.Text = "forward";
                                       btnSend_Click(null, null);
                                   }
                               }
                               else
                               {
                                   if (Jyf.Source != JfU)
                                   {
                                       Jyf.Source = JfU;
                                       txtMessage.Text = "forwardREL";
                                       btnSend_Click(null, null);
                                   }
                               }

                               //----------Reverse
                               if (LTY >= -1 && LTY < -0.95 && LTX <= 0.55 && LTX >= -0.55)
                               {
                                   if (Jyb.Source == JbU)
                                   {
                                       Jyb.Source = JbD;
                                       txtMessage.Text = "reverse";
                                       btnSend_Click(null, null);
                                   }
                               }
                               else
                               {
                                   if (Jyb.Source != JbU)
                                   {
                                       Jyb.Source = JbU;
                                       txtMessage.Text = "reverseREL";
                                       btnSend_Click(null, null);
                                   }
                               }

                               //----------Top Right
                               if (LTY <= 0.95 && LTY > 0.55 && LTX <= 0.95 && LTX > 0.55)
                               {
                                   if (JDtr.Source == JtrU)
                                   {
                                       JDtr.Source = JtrD;
                                       txtMessage.Text = "tr";
                                       btnSend_Click(null, null);
                                   }
                               }
                               else
                               {
                                   if (JDtr.Source != JtrU)
                                   {
                                       JDtr.Source = JtrU;
                                       txtMessage.Text = "trREL";
                                       btnSend_Click(null, null);
                                   }
                               }

                               //----------Top Left
                               if (LTY <= 0.95 && LTY > 0.55 && LTX >= -0.95 && LTX < -0.55)
                               {
                                   if (JDtl.Source == JtlU)
                                   {
                                       JDtl.Source = JtlD;
                                       txtMessage.Text = "tl";
                                       btnSend_Click(null, null);
                                   }
                               }
                               else
                               {
                                   if (JDtl.Source != JtlU)
                                   {
                                       JDtl.Source = JtlU;
                                       txtMessage.Text = "tlREL";
                                       btnSend_Click(null, null);
                                   }
                               }

                               //----------Bottom Right
                               if (LTY >= -0.95 && LTY < -0.55 && LTX <= 0.95 && LTX > 0.55)
                               {
                                   if (JDbr.Source == JbrU)
                                   {
                                       JDbr.Source = JbrD;
                                       txtMessage.Text = "br";
                                       btnSend_Click(null, null);
                                   }
                               }
                               else
                               {
                                   if (JDbr.Source != JbrU)
                                   {
                                       JDbr.Source = JbrU;
                                       txtMessage.Text = "brREL";
                                       btnSend_Click(null, null);
                                   }
                               }

                               //----------Bottom Left
                               if (LTY >= -0.95 && LTY < -0.55 && LTX >= -0.95 && LTX < -0.55)
                               {
                                   if (JDbl.Source == JblU)
                                   {
                                       JDbl.Source = JblD;
                                       txtMessage.Text = "bl";
                                       btnSend_Click(null, null);
                                   }
                               }
                               else
                               {
                                   if (JDbl.Source != JblU)
                                   {
                                       JDbl.Source = JblU;
                                       txtMessage.Text = "blREL";
                                       btnSend_Click(null, null);
                                   }
                               }

                               //----------Increase Speed
                               if (RTY <= 1 && RTY > 0.95 && RTX <= 0.55 && RTX >= -0.55)
                               {
                                  
                                   txtMessage.Text = "inc";
                                   _socket.Send(txtMessage.Text);

                               }
                             
                             

                               //----------Decrease Speed
                               if (RTY >= -1 && RTY < -0.95 && RTX <= 0.55 && RTX >= -0.55)
                               {
                                 
                                   txtMessage.Text = "dec";
                                   _socket.Send(txtMessage.Text);

                               }
                           


                               //---------enable line
                               if (reading.Buttons == GamepadButtons.A)
                               {
                                   if (enable == 1)
                                   {
                                       enable = 0;
                                       txtMessage.Text = "enable";
                                       _socket.Send(txtMessage.Text);
                                   }
                               }
                               else
                               {
                                   if (enable != 1)
                                   {
                                       enable = 1;
                                   }
                               }

                               //Sends a "check" message as a fail safe if network connection is lost
                               Task.Delay(TimeSpan.FromMilliseconds(100));
                               txtMessage.Text = "check";
                               _socket.Send(txtMessage.Text);
                           });

                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }
        }

        //Updates UI if Controller Disconnects
        private async void Gamepad_GamepadRemoved(object sender, Gamepad e)
        {
            _Gamepad = null;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { tbConnected.Text = "Controller removed"; });
        }

        //Updates UI if Controller Connects
        private async void Gamepad_GamepadAdded(object sender, Gamepad e)
        {
            _Gamepad = e;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { tbConnected.Text = "Controller added"; });
        }
    }
}