using ServerSocket;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

// Test program for Pulse Train Hat http://www.pthat.com

namespace PulseTrainHatMecanum
{
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Private variables
        /// </summary>
        private SerialDevice serialPort = null;

        private DataWriter dataWriteObject = null;
        private DataReader dataReaderObject = null;

        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        private SocketServer socket;

        private DispatcherTimer timer;
        private int tick = 0;

        // Emergency Stop Gpio Pin
        private GpioPin ESpin;
        private const int ES_PIN = 12;

       

        public static class MyStaticValues
        {
                       
            //----Button status:
            // 0: pressed
            // 1: enabled
            // 2: disabled
            public static int Enable = 2;

            //Switch case to determine which direction is active
            public static string Movement_Direction = "";

            //Switch case for whether a button is pressed or released
            public static string Movement_Action = "";

            //stores set axis command
            public static string Xsendstore;
            public static string Ysendstore;
            public static string Zsendstore;
            public static string Esendstore;

            //Catches a button release event
            public static int ACatch;

            //Flag for all axis complete
            public static int setback = 0;

            //Speed Increment
            public static double inc = 0.005;

            //Flag for speed change
            public static int spdchange = 1;

            //Flag for if motors are running
            public static int running;

            //Flag for checking network socket is active
            public static int Netcheck;

        }

        public MainPage()
        {
            this.InitializeComponent();

            //Enable/Disable Controls
            MyStaticValues.Enable = 2;
            MyStaticValues.ACatch = 0;
            comPortInput.IsEnabled = false;
            Firmware1.IsEnabled = false;
            LowSpeedBaud.IsChecked = true;
            HighSpeedBaud.IsChecked = false;
            Reset.IsEnabled = false;
            ToggleEnableLine.IsEnabled = false;

            //Format Boxes
            calculatetravelspeeds();

            //Collect List of Devices
            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();

            //Initiate Network Socket
            startsocket();

            //Set up Timer
            timer = new DispatcherTimer();
            timer.Tick += Ticker;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);

            //Initialise GPIO pins
            InitGPIO();
        }

        //Initialise Gpio Pins
        private void InitGPIO()
        {
            // Assigns the pin number that was declared in our variables to the pin ESpin
            ESpin = GpioController.GetDefault().OpenPin(ES_PIN);

            // Set Pin to Output
            ESpin.SetDriveMode(GpioPinDriveMode.Output);

            // Set a debounce timeout to filter out switch bounce noise from a button press
            ESpin.DebounceTimeout = TimeSpan.FromMilliseconds(100);
        }

        //Change Speed whilst running method
        private void ChangeSpeed()
        {
            //Declare local variables
            string tmp;
            string tmpHZ = HZresult.Text;
            

            //Store substring of axis sendstore
            tmp = MyStaticValues.Ysendstore.Substring(6, 10);

            //Sendstore is not null
            if (tmp != "000000.000")
            {
                //Store Change Axis speed on the fly Command
                MyStaticValues.Ysendstore = "I00QY" + tmpHZ + "*";
            }

            tmp = MyStaticValues.Zsendstore.Substring(6, 10);
            if (tmp != "000000.000")
            {
                MyStaticValues.Zsendstore = "I00QZ" + tmpHZ + "*";
            }

            tmp = MyStaticValues.Esendstore.Substring(6, 10);
            if (tmp != "000000.000")
            {
                MyStaticValues.Esendstore = "I00QE" + tmpHZ + "*";
            }

            tmp = MyStaticValues.Xsendstore.Substring(6, 10);
            if (tmp != "000000.000")
            {
                MyStaticValues.Xsendstore = "I00QX" + tmpHZ + "*";
                
                //Send Command
                sendText.Text = "I00QX" + tmpHZ + "*";
                SendDataout();
            }
        }

        //Dispatch Timer Ticker
        private void Ticker(object sender, object e)
        {
            //Increment ticker
            tick++;

            //Ticker has elapsed 200ms
            if (tick > 2)
            {
                //Network connection is still active
                if (MyStaticValues.Netcheck == 1)
                {
                    //Reset Flag
                    MyStaticValues.Netcheck = 0;
                }
                else
                {
                    //Set Pin low to call emergency stop function on the PTHAT
                    ESpin.Write(GpioPinValue.Low);

                    //Stop timer
                    timer.Stop();
                }

                //Reset tick
                tick = 0;
            }
        }

        //Initialise Network Socket
        public async void startsocket()
        {
            //Designate Port
            socket = new SocketServer(9000);
            await ThreadPool.RunAsync(x =>
            {
               
                socket.OnDataRecived += Socket_OnDataRecived;
                socket.Star();
            });
        }

        //Incoming Socket Data Recieved
        private async void Socket_OnDataRecived(string data)
        {
            //Tells Socket Application we have recieved data
          //  socket.Send("Text Recive:" + data);

            // Wait for when UI is ready to be updated
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                                               
                //Store incoming data as string
                string checker = Convert.ToString(data);
                    
                //Determines what data has been recieved            
                switch (checker)
                {
                    //Right Pressed
                    case "right":
                        //Calls Method
                        Dir_Right_Press();
                        break;

                    //Right Released
                    case "rightREL":
                        Dir_Right_Release();
                        break;

                    case "left":
                        Dir_Left_Press();
                        break;

                    case "leftREL":
                        Dir_Left_Release();
                        break;

                    case "forward":
                        Dir_Forward_Press();
                        break;

                    case "forwardREL":
                        Dir_Forward_Release();
                        break;

                    case "reverse":
                        Dir_Reverse_Press();
                        break;

                    case "reverseREL":
                        Dir_Reverse_Release();
                        break;

                    case "tr":
                        Dir_TopRight_Press();
                        break;

                    case "trREL":
                        Dir_TopRight_Release();
                        break;

                    case "tl":
                        Dir_TopLeft_Press();
                        break;

                    case "tlREL":
                        Dir_TopLeft_Release();
                        break;

                    case "br":
                        Dir_BottomRight_Press();
                        break;

                    case "brREL":
                        Dir_BottomRight_Release();
                        break;

                    case "bl":
                        Dir_BottomLeft_Press();
                        break;

                    case "blREL":
                        Dir_BottomLeft_Release();
                        break;

                    case "cw":
                        Dir_RotateCW_Press();
                        break;

                    case "cwREL":
                        Dir_RotateCW_Release();
                        break;

                    case "ccw":
                        Dir_RotateCCW_Press();
                        break;

                    case "ccwREL":
                        Dir_RotateCCW_Release();
                        break;

                    //Speed Increase
                    case "inc":
                        
                        //Limits Max Speed to 100km/h
                        if (Convert.ToDouble(Travel_Speed.Text) < 100)
                        {
                            //Increments speed
                            Travel_Speed.Text = Convert.ToString(Convert.ToDouble(Travel_Speed.Text) + MyStaticValues.inc);

                            //Sends speed data accross network 
                            socket.Send(Travel_Speed.Text);

                            //Motors are running
                            if (MyStaticValues.running == 1)
                            {
                                //Speed update is enabled
                                if (MyStaticValues.spdchange == 1)
                                {
                                    //Set flag to disable
                                    MyStaticValues.spdchange = 0;

                                    //Call Change speed method
                                    ChangeSpeed();
                                }
                            }
                        }
                        else
                        {
                            Travel_Speed.Text = "100";
                        }
                        break;

                    //Speed Decrease
                    case "dec":
                        if (Convert.ToDouble(Travel_Speed.Text) > 0)
                        {
                            Travel_Speed.Text = Convert.ToString(Convert.ToDouble(Travel_Speed.Text) - MyStaticValues.inc);
                            socket.Send(Travel_Speed.Text);

                            if (MyStaticValues.running == 1)
                            {
                                if (MyStaticValues.spdchange == 1)
                                {
                                    MyStaticValues.spdchange = 0;
                                    ChangeSpeed();
                                }
                            }
                        }
                        else
                        {
                            Travel_Speed.Text = "0";
                        }
                        break;
                                       

                    case "enable":
                        ToggleEnableLine_Click(null, null);
                        break;

                        //Poll to see if Network Connection is still active
                    case "check":

                        //Timer is not active
                        if (timer.IsEnabled == false)
                        {
                            //Sends an enable Emergency Stop and limits Command
                            sendText.Text = "*I00KS1*";
                            SendDataout();

                            //Starts timer
                            timer.Start();
                        }
                        //Sets flag to active
                        MyStaticValues.Netcheck = 1;
                        break;
                }
            });
        }

       

        /// <summary>
        /// ListAvailablePorts
        /// - Use SerialDevice.GetDeviceSelector to enumerate all serial devices
        /// - Attaches the DeviceInformation to the ListBox source so that DeviceIds are displayed
        /// </summary>
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                status.Text = "Select a device and connect";

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }

                DeviceListSource.Source = listOfDevices;
                comPortInput.IsEnabled = true;
                ConnectDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        /// <summary>
        /// comPortInput_Click: Action to take when 'Connect' button is clicked
        /// - Get the selected device index and use Id to create the SerialDevice object
        /// - Configure default settings for the serial port
        /// - Create the ReadCancellationTokenSource token
        /// - Start listening on the serial port input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void comPortInput_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            if (selection.Count <= 0)
            {
                status.Text = "Select a device and connect";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];

            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);

                // Disable the 'Connect' button
                comPortInput.IsEnabled = false;

                // Configure serial settings
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(30);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(30);

                if (LowSpeedBaud.IsChecked == true)
                {
                    serialPort.BaudRate = 115200;
                }
                else
                {
                    serialPort.BaudRate = 806400;
                }

                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

                // Display configured settings
                status.Text = "Serial port configured successfully: ";
                status.Text += serialPort.BaudRate + "-";
                status.Text += serialPort.DataBits + "-";
                status.Text += serialPort.Parity.ToString() + "-";
                status.Text += serialPort.StopBits;

                // Set the RcvdText field to invoke the TextChanged callback
                // The callback launches an async Read task to wait for data
                rcvdText.Text = "Waiting for data...";

                // Create cancellation token object to close I/O operations when closing the device
                ReadCancellationTokenSource = new CancellationTokenSource();

                // Enable 'Start' button to allow sending data
                MyStaticValues.Enable = 1;
                MyStaticValues.ACatch = 0;
                Firmware1.IsEnabled = true;
                Reset.IsEnabled = true;
                ToggleEnableLine.IsEnabled = true;
                sendText.Text = "";

                //Emulate Emergency Stop Pin
                ESpin.Write(GpioPinValue.High);

                Listen();
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                comPortInput.IsEnabled = true;
                MyStaticValues.Enable = 2;
                MyStaticValues.ACatch = 0;
                Firmware1.IsEnabled = false;
                Reset.IsEnabled = false;
                ToggleEnableLine.IsEnabled = false;
            }
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync()
        {
            Task<UInt32> storeAsyncTask;

            // Load the text from the sendText input text box to the dataWriter object
            dataWriteObject.WriteString(sendText.Text);

            // Launch an async task to complete the write operation
            storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

            UInt32 bytesWritten = await storeAsyncTask;
            if (bytesWritten > 0)
            {
                status.Text = sendText.Text + ", ";
                status.Text += "bytes written successfully!";
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    status.Text = "Reading task was cancelled, closing device and cleaning up";
                    CloseDevice();
                }
                else
                {
                    status.Text = ex.Message;
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        //private async Task ReadAsync()
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            //    loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask();

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                rcvdText.Text = dataReaderObject.ReadString(bytesRead);
                string input = rcvdText.Text;

                Debug.WriteLine(rcvdText.Text);

                //Check if received message can be divided by 7 as our return messages are 7 bytes long
                if (input.Length % 7 == 0)

                {
                    for (int i = 0; i < input.Length; i += 7)
                    //  foreach (string match in sub)

                    {
                        string sub = input.Substring(i, 7);

                      
                        //Check if Start ALL command Received
                        if (sub == "RI00SA*")
                        {
                            //Enable/Disable certain controls
                            ToggleEnableLine.IsEnabled = false;
                            Firmware1.IsEnabled = false;                            
                            if (MyStaticValues.ACatch == 1)
                            {
                                MyStaticValues.Enable = 2;
                                MyStaticValues.Movement_Action = "released";
                                InitMovement();
                            }
                            MyStaticValues.ACatch = 1;

                            MyStaticValues.running = 1;
                        }

                        //Check if Set X Axis completed
                        if (sub == "CI00CX*")
                        {
                            //Sends Set Y Axis Command
                            sendText.Text = MyStaticValues.Ysendstore;
                            SendDataout();
                        }

                        //Check if Set Y Axis completed
                        if (sub == "CI00CY*")
                        {
                            sendText.Text = MyStaticValues.Zsendstore;
                            SendDataout();
                        }

                        //Check if Set Z Axis completed
                        if (sub == "CI00CZ*")
                        {
                            sendText.Text = MyStaticValues.Esendstore;
                            SendDataout();
                        }

                        //Check if Set E Axis completed
                        if (sub == "CI00CE*")
                        {
                            sendText.Text = "I00SA*";
                            SendDataout();
                        }

                        //Check if X Axis completed amount of pulses
                        if (sub == "CI00SX*")
                        {
                            //Increment Flag
                            MyStaticValues.setback += 1;

                            //Flag is active
                            if (MyStaticValues.setback == 4)
                            {
                                //Reset Variables
                                MyStaticValues.Enable = 1;
                                MyStaticValues.ACatch = 0;
                                MyStaticValues.setback = 0;
                            }
                        }

                        //Check if Y Axis completed amount of pulses
                        if (sub == "CI00SY*")
                        {
                            MyStaticValues.setback += 1;

                            if (MyStaticValues.setback == 4)
                            {
                                MyStaticValues.Enable = 1;
                                MyStaticValues.ACatch = 0;
                                MyStaticValues.setback = 0;
                            }
                        }

                        //Check if Z Axis completed amount of pulses
                        if (sub == "CI00SZ*")
                        {
                            MyStaticValues.setback += 1;

                            if (MyStaticValues.setback == 4)
                            {
                                MyStaticValues.Enable = 1;
                                MyStaticValues.ACatch = 0;
                                MyStaticValues.setback = 0;
                            }
                        }

                        //Check if E Axis completed amount of pulses
                        if (sub == "CI00SE*")
                        {
                            MyStaticValues.setback += 1;

                            if (MyStaticValues.setback == 4)
                            {
                                MyStaticValues.Enable = 1;
                                MyStaticValues.ACatch = 0;
                                MyStaticValues.setback = 0;
                            }
                        }

                        //Check For Firmware reply Back
                        if (sub == "RI00FW*")
                        {
                            rcvdText.Text = rcvdText.Text.Substring(i + 8, 40);
                        }

                        //Check if ALL Axis Stop button Complete
                        if (sub == "RI00TA*")
                        {

                            if (MyStaticValues.ACatch == 1)
                            {
                                ToggleEnableLine.IsEnabled = true;
                                Firmware1.IsEnabled = true;
                                MyStaticValues.running = 0;
                            }
                        }

                        //X Change Axis Speed Recieved
                        if (sub == "RI00QX*")
                        {
                            sendText.Text = MyStaticValues.Ysendstore;
                            SendDataout();
                        }

                        //Y Change Axis Speed Recieved
                        if (sub == "RI00QY*")
                        {
                            sendText.Text = MyStaticValues.Zsendstore;
                            SendDataout();
                        }

                        //Z Change Axis Speed Recieved
                        if (sub == "RI00QZ*")
                        {
                            sendText.Text = MyStaticValues.Esendstore;
                            SendDataout();
                        }

                        //E Change Axis Speed Recieved
                        if (sub == "RI00QE*")
                        {
                            //Enable speed change flag
                            await Task.Delay(10);
                            MyStaticValues.spdchange = 1;
                        }
                    } //End of checking length if
                } //End of checking for bytes
            } //End of byte read
        } //End of async read

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;
            comPortInput.IsEnabled = true;
            rcvdText.Text = "";
            listOfDevices.Clear();
        }

        /// <summary>
        /// closeDevice_Click: Action to take when 'Disconnect and Refresh List' is clicked on
        /// - Cancel all read operations
        /// - Close and dispose the SerialDevice object
        /// - Enumerate connected devices
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeDevice_Click(object sender, RoutedEventArgs e)
        {
            Disconnectserial();
        }

        private void Disconnectserial()
        {
            try
            {
                status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();
                Firmware1.IsEnabled = false;
                Reset.IsEnabled = false;
                MyStaticValues.Enable = 2;
                MyStaticValues.ACatch = 0;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        private void Firmware_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00FW*";
            SendDataout();
        }

        private async void SendDataout()
        {
            try
            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync();
                }
                else
                {
                    status.Text = "Select a device and connect";
                }
            }
            catch (Exception ex)
            {
                status.Text = "Send Data: " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "N*";
            SendDataout();
            Disconnectserial();
        }

        private void Travel_Speed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(Travel_Speed.Text.Trim()))
            {
                calculatetravelspeeds();
            }
        }

        private void PulsesPerRev_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(PulsesPerRev.Text.Trim()))
            {
                calculatetravelspeeds();
            }
        }

        private void Wheel_Diameter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(Wheel_Diameter.Text.Trim()))
            {
                calculatetravelspeeds();
            }
        }

        private void Speed_increment_txt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(Speed_increment_txt.Text.Trim()))
            {
                MyStaticValues.inc = Convert.ToDouble(Speed_increment_txt.Text);
            }
        }

        private void calculatetravelspeeds()
        {
            double tmp = (Convert.ToDouble(Wheel_Diameter.Text) * Math.PI) / 1000000;
            HZresult.Text = String.Format("{0:000000.000}", (((Convert.ToDouble(Travel_Speed.Text)) / tmp) / 3600) * Convert.ToDouble(PulsesPerRev.Text));        
            Resolution.Text = Convert.ToString(1.0 / Convert.ToDouble(PulsesPerRev.Text));
        }

        private void ToggleEnableLine_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00HT*";
            SendDataout();
        }

        //--------------------------------Movement Code----------------------------------//
        //Forward has been pressed
        private void Forward_Dir_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_Forward_Press();
        }

        private void Dir_Forward_Press()
        {
            //Set Movement to pressed
            MyStaticValues.Enable = 0;

            //Determine Direction
            MyStaticValues.Movement_Direction = "Forward";

            //Sets Action as a press
            MyStaticValues.Movement_Action = "pressed";


            MyStaticValues.ACatch = 0;

            //initialises Movement set method
            InitMovement();
        }

        //Forward has been released
        private void Forward_Dir_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_Forward_Release();
        }

        private void Dir_Forward_Release()
        {
            //Forward has triggered a release
            if (MyStaticValues.ACatch == 1)
            {
                //Movement has been Pressed
                if (MyStaticValues.Enable == 0)
                {
                    //Movement is Disabled
                    MyStaticValues.Enable = 2;

                    //Determine Direction
                    MyStaticValues.Movement_Direction = "Forward";

                    //Sets Action as a release
                    MyStaticValues.Movement_Action = "released";

                    //initialises Movement set method
                    InitMovement();
                }
            }
            else
            {
                //Set to trigger a release
                MyStaticValues.ACatch = 1;
            }
        }

        //Reverse has been pressed
        private void Reverse_Dir_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_Reverse_Press();
        }

        private void Dir_Reverse_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "Reverse";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Reverse has been released
        private void Reverse_Dir_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_Reverse_Release();
        }

        private void Dir_Reverse_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "Reverse";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }

        //Right has been pressed
        private void Right_Dir_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_Right_Press();
        }

        private void Dir_Right_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "Right";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Right has been released
        private void Right_Dir_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_Right_Release();
        }

        private void Dir_Right_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "Right";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }

        //Left has been pressed
        private void Left_Dir_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_Left_Press();
        }

        private void Dir_Left_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "Left";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Left has been released
        private void Left_Dir_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_Left_Release();
        }

        private void Dir_Left_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "Left";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }

        //Rotate Counterclockwise has been pressed
        private void Rotate_CCW_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_RotateCCW_Press();
        }

        private void Dir_RotateCCW_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "Counterclockwise";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Rotate Counterclockwise has been released
        private void Rotate_CCW_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_RotateCCW_Release();
        }

        private void Dir_RotateCCW_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "Counterclockwise";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }

        //Rotate Clockwise has been pressed
        private void Rotate_CW_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_RotateCW_Press();
        }

        private void Dir_RotateCW_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "Clockwise";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Rotate Clockwise has been released
        private void Rotate_CW_release(object sender, PointerRoutedEventArgs e)

        {
            Dir_RotateCW_Release();
        }

        private void Dir_RotateCW_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "Clockwise";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }

        //Top Left has been pressed
        private void TopLeft_Dir_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_TopLeft_Press();
        }

        private void Dir_TopLeft_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "TopLeft";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Top Left has been released
        private void TopLeft_Dir_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_TopLeft_Release();
        }

        private void Dir_TopLeft_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "TopLeft";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }

        //Top Right has been pressed
        private void TopRight_Dir_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_TopRight_Press();
        }

        private void Dir_TopRight_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "TopRight";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Top Right has been released
        private void TopRight_Dir_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_TopRight_Release();
        }

        private void Dir_TopRight_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "TopRight";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }

        //Bottom Left has been pressed
        private void BottomLeft_Dir_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_BottomLeft_Press();
        }

        private void Dir_BottomLeft_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "BottomLeft";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Bottom Left has been released
        private void BottomLeft_Dir_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_BottomLeft_Release();
        }

        private void Dir_BottomLeft_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "BottomLeft";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }

        //Bottom Right has been pressed
        private void BottomRight_Dir_press(object sender, PointerRoutedEventArgs e)
        {
            Dir_BottomRight_Press();
        }

        private void Dir_BottomRight_Press()
        {
            if (MyStaticValues.Enable == 1)
            {
                MyStaticValues.Enable = 0;
                MyStaticValues.Movement_Direction = "BottomRight";
                MyStaticValues.Movement_Action = "pressed";
                MyStaticValues.ACatch = 0;
                InitMovement();
            }
        }

        //Bottom Right has been released
        private void BottomRight_Dir_release(object sender, PointerRoutedEventArgs e)
        {
            Dir_BottomRight_Release();
        }

        private void Dir_BottomRight_Release()
        {
            if (MyStaticValues.ACatch == 1)
            {
                if (MyStaticValues.Enable == 0)
                {
                    MyStaticValues.Enable = 2;
                    MyStaticValues.Movement_Direction = "BottomRight";
                    MyStaticValues.Movement_Action = "released";
                    InitMovement();
                }
            }
            else
            {
                MyStaticValues.ACatch = 1;
            }
        }



        //Pointer has exited object Left_Dir
        private void Left_Dir_Exit(object sender, PointerRoutedEventArgs e)
        {
            //Movement has been pressed
            if (MyStaticValues.Enable == 0)
            {
                //calls Left release method
                Dir_Left_Release();
            }
        }

        //Pointer has exited object Right_Dir
        private void Right_Dir_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_Right_Release();
            }
        }

        //Pointer has exited object Forward_Dir
        private void Forward_Dir_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_Forward_Release();
            }
        }

        //Pointer has exited object Reverse_Dir
        private void Reverse_Dir_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_Reverse_Release();
            }
        }

        //Pointer has exited object Rotate_CCW
        private void Rotate_CCW_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_RotateCCW_Release();
            }
        }

        //Pointer has exited object Rotate_CW
        private void Rotate_CW_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_RotateCW_Release();
            }
        }

        //Pointer has exited object TopRight_Dir
        private void TopRight_Dir_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_TopRight_Release();
            }
        }

        //Pointer has exited object BottomRight_Dir
        private void BottomRight_Dir_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_BottomRight_Release();
            }
        }

        //Pointer has exited object BottomLeft_Dir
        private void BottomLeft_Dir_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_BottomLeft_Release();
            }
        }

        //Pointer has exited object TopLeft_Dir
        private void TopLeft_Dir_Exit(object sender, PointerRoutedEventArgs e)
        {
            if (MyStaticValues.Enable == 0)
            {
                Dir_TopLeft_Release();
            }
        }

        //Method that initialises Movement
        private async void InitMovement()
        {
            //Axis Direction
            string Dir = "";

            //Determines whether the button is pressed or released
            switch (MyStaticValues.Movement_Action)
            {
                //Button is pressed
                case "pressed":

                    //determines which direction to start
                    switch (MyStaticValues.Movement_Direction)
                    {
                        case "Forward":

                            //sets image to Button Down
                            BitmapImage j = new BitmapImage(new Uri("ms-appx:///Assets/Up_D.png"));
                            Forward_Dir.Source = j;
                         
                            //sets the pin direction
                            Dir = PinY.Text;

                            //Stores Send Command
                            sendText.Text = "I00C" + "Y" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = PinZ.Text;
                            sendText.Text = "I00C" + "Z" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = PinE.Text;
                            sendText.Text = "I00C" + "E" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = PinX.Text;
                            sendText.Text = "I00C" + "X" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "Reverse":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/Down_D.png"));
                            Reverse_Dir.Source = j;
                     
                          
                            Dir = (PinY.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "Y" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = (PinZ.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "Z" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = (PinE.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "E" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = (PinX.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "X" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "Left":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/Left_D.png"));
                            Left_Dir.Source = j;
                      
                          
                            Dir = (PinY.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "Y" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = (PinZ.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "Z" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = PinE.Text;
                            sendText.Text = "I00C" + "E" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = PinX.Text;
                            sendText.Text = "I00C" + "X" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "Right":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/Right_D.png"));
                            Right_Dir.Source = j;
                       
                          
                            Dir = PinY.Text;
                            sendText.Text = "I00C" + "Y" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = PinZ.Text;
                            sendText.Text = "I00C" + "Z" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = (PinE.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "E" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = (PinX.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "X" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "Counterclockwise":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/CCWD.png"));
                            Rotate_CCW.Source = j;
                        
                         
                            Dir = (PinY.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "Y" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = PinZ.Text;
                            sendText.Text = "I00C" + "Z" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = PinE.Text;
                            sendText.Text = "I00C" + "E" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = (PinX.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "X" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "Clockwise":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/CWD.png"));
                            Rotate_CW.Source = j;
                        
                          
                            Dir = PinY.Text;
                            sendText.Text = "I00C" + "Y" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = (PinZ.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "Z" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = (PinE.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "E" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = PinX.Text;
                            sendText.Text = "I00C" + "X" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "TopRight":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/trD.png"));
                            TopRight_Dir.Source = j;
                      
                          
                            Dir = PinY.Text;
                            sendText.Text = "I00C" + "Y" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = PinZ.Text;
                            sendText.Text = "I00C" + "Z" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = PinE.Text;
                            sendText.Text = "I00C" + "E" + "000000.000" + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = PinX.Text;
                            sendText.Text = "I00C" + "X" + "000000.000" + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "TopLeft":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/tlD.png"));
                            TopLeft_Dir.Source = j;
                          
                           
                            Dir = PinY.Text;
                            sendText.Text = "I00C" + "Y" + "000000.000" + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = PinZ.Text;
                            sendText.Text = "I00C" + "Z" + "000000.000" + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = PinE.Text;
                            sendText.Text = "I00C" + "E" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = PinX.Text;
                            sendText.Text = "I00C" + "X" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "BottomLeft":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/blD.png"));
                            BottomLeft_Dir.Source = j;
                         
                          
                            Dir = (PinY.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "Y" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = (PinZ.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "Z" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = PinE.Text;
                            sendText.Text = "I00C" + "E" + "000000.000" + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = PinX.Text;
                            sendText.Text = "I00C" + "X" + "000000.000" + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;

                        case "BottomRight":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/brD.png"));
                            BottomRight_Dir.Source = j;
                          
                           
                            Dir = PinY.Text;
                            sendText.Text = "I00C" + "Y" + "000000.000" + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Ysendstore = sendText.Text;

                            Dir = PinZ.Text;
                            sendText.Text = "I00C" + "Z" + "000000.000" + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Zsendstore = sendText.Text;

                            Dir = (PinE.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "E" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Esendstore = sendText.Text;

                            Dir = (PinX.Text == "1") ? "0" : "1";
                            sendText.Text = "I00C" + "X" + String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text)) + "0001000000" + Dir + "11" + String.Format("{0:000}", Convert.ToDouble(RampDivide.Text)) + String.Format("{0:000}", Convert.ToDouble(RampPause.Text)) + "0" + String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text)) + "*";
                            MyStaticValues.Xsendstore = sendText.Text;
                            SendDataout();

                            break;
                    }
                                       
                    break;

                // determines that the button is released
                case "released":

                    //determines which direction to stop
                    switch (MyStaticValues.Movement_Direction)
                    {
                        case "Forward":

                            //sets the image to button released
                            BitmapImage j = new BitmapImage(new Uri("ms-appx:///Assets/Up.png"));
                            Forward_Dir.Source = j;

                            break;

                        case "Reverse":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/Down.png"));
                            Reverse_Dir.Source = j;                          

                            break;

                        case "Left":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/Left.png"));
                            Left_Dir.Source = j;
                           
                            break;

                        case "Right":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/Right.png"));
                            Right_Dir.Source = j;
                           
                            break;

                        case "Counterclockwise":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/CCWU.png"));
                            Rotate_CCW.Source = j;
                          
                            break;

                        case "Clockwise":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/CWU.png"));
                            Rotate_CW.Source = j;
                           
                            break;

                        case "TopRight":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/trU.png"));
                            TopRight_Dir.Source = j;
                         
                            break;

                        case "TopLeft":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/tlU.png"));
                            TopLeft_Dir.Source = j;
                          
                            break;

                        case "BottomLeft":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/blU.png"));
                            BottomLeft_Dir.Source = j;
                          
                            break;

                        case "BottomRight":

                            j = new BitmapImage(new Uri("ms-appx:///Assets/brU.png"));
                            BottomRight_Dir.Source = j;
                           
                            break;
                    }

                    //sends out a stop all command
                    sendText.Text = "I00TA*";
                    SendDataout();

                    break;
            }
        }

       
    }
}