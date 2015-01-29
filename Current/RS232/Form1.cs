using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.Text.RegularExpressions;
using bert;
using System.Reflection;
using System.Linq.Expressions;

namespace RS232
{
    public partial class fclsRS232Tester : Form
    {
        string InputData = String.Empty;
        
        // This delegate enables asynchronous calls for setting
        // the text property on a TextBox control:
        delegate void SetTextCallback(string text);
 
        public fclsRS232Tester()
        {
            InitializeComponent();

            // Nice methods to browse all available ports:
            string[] ports = SerialPort.GetPortNames();

            // Add all port names to the combo box:
            foreach (string port in ports)
            {
                cmbComSelect.Items.Add(port);
            }
        }
        public const int MESSAGEMAXLENGTH = 100;
        public const int MESSAGEMINLENGTH = 10;
        public const int NEWSTARTFRAMEFOUND = 0;
        public const int NOSTARTFRAMEFOUND = 1;
        public const int COMPLETEFRAMEFOUND = 2;
        public const int INCOMPLETEFRAMEFOUND = 4;
        public const int MESSAGEGPRMC = 20;
        public const int MESSAGEGPGGA = 21;
        public const int MESSAGEGPGSA = 22;
        
        
        Constants definedConstants = new Constants();

        private void cmbComSelect_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (port.IsOpen) port.Close();
            port.PortName = cmbComSelect.SelectedItem.ToString();
            stsStatus.Text = port.PortName + ": 9600,8N1";

            // try to open the selected port:
            try
            {
                port.Open();
            }
            // give a message, if the port is not available:
            catch
            {
                MessageBox.Show("Serial port " + port.PortName + " cannot be opened!", "RS232 tester", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbComSelect.SelectedText = "";
                stsStatus.Text = "Select serial port!";
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (port.IsOpen) port.WriteLine(txtOut.Text);
            else MessageBox.Show("Serial port is closed!", "RS232 tester", MessageBoxButtons.OK, MessageBoxIcon.Error);
            txtOut.Clear();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtIn.Clear();
        }




        char[] Rx_Buffer = new char[MESSAGEMAXLENGTH];
        char[] Rx_BufferBufferd = new char[MESSAGEMAXLENGTH];
        
        byte buffindex = 0;
        byte result = 10;
        char Lastread;
        private void Port_DataReceived2(object sender, SerialDataReceivedEventArgs e)
        {
            Lastread = (char)port.ReadChar(); // .ReadByte();
            Rx_Buffer[buffindex] = Lastread;

            result = Parsingdata(buffindex);
            switch (result)
            {
                case NEWSTARTFRAMEFOUND:
                    Rx_Buffer[0] = Rx_Buffer[buffindex];
                    buffindex = 0;
                    break;
                case NOSTARTFRAMEFOUND:
                    break;
                case INCOMPLETEFRAMEFOUND:
                    break;
                case COMPLETEFRAMEFOUND:
                    Array.Clear(Rx_Buffer, (buffindex + 1), (Rx_Buffer.Length - (buffindex+1)));
                    Array.Copy(Rx_Buffer, Rx_BufferBufferd, buffindex+1);
                    Array.Clear(Rx_BufferBufferd, (buffindex + 1), (Rx_BufferBufferd.Length - (buffindex + 1)));
                    buffindex = 0;
                    string s = new string(Rx_BufferBufferd);
                    SetText_decoded(s);
                    Newframefound();
                    break;
                default:
                    break;
            }
            
            if (buffindex + 1 < MESSAGEMAXLENGTH) 
                buffindex++;
            else 
                buffindex = 0;
            SetText(Lastread.ToString());
  
        }

        /// <summary>
        /// Parsing the data. Check for complete message
        /// </summary>
        /// <returns>
        /// complete / incomplete frame received
        /// </returns>
        private byte Parsingdata(byte buffindex)
        {
            if (Rx_Buffer[buffindex] == (char)'$') 
            {
                return NEWSTARTFRAMEFOUND;
            }
            else if (Rx_Buffer[0] != (char)'$')
            {
                return NOSTARTFRAMEFOUND; 
            }
            // wel begin
            else if (Rx_Buffer[buffindex] == (char)'\n') /* next line windows: \r \n */
            {
                return COMPLETEFRAMEFOUND; // compleet frame gevonden
            }
            else
                return INCOMPLETEFRAMEFOUND;
        }


        private  void Newframefound()
        {
            int Messagetype = Getmessagetype();
            switch (Messagetype)
            {
                default:
                    break;
                case MESSAGEGPRMC:
                    Decodemessage_GPRMC();
                    break;
                case MESSAGEGPGGA:
                    Decodemessage_GPGGA();
                    break;
                case MESSAGEGPGSA:
                    Decodemessage_GPGSA();
                    break;
            }
        }
        /// <summary> Decoding message type. </summary>
        /// <returns> Messagetype </returns>
        private int Getmessagetype()
        {
            int index = 0; // && = Short-circuit AND. if one is false other is not executed.

            if ((Rx_BufferBufferd[index = 1] == (char)'G') && (Rx_BufferBufferd[++index] == (char)'P') && (Rx_BufferBufferd[++index] == (char)'R') &&
                (Rx_BufferBufferd[++index] == (char)'M') && (Rx_BufferBufferd[++index] == (char)'C'))
                return MESSAGEGPRMC;
            else if ((Rx_BufferBufferd[index =1 ] == (char)'G') && (Rx_BufferBufferd[++index] == (char)'P') && (Rx_BufferBufferd[++index] == (char)'G') &&
                (Rx_BufferBufferd[++index] == (char)'G') && (Rx_BufferBufferd[++index] == (char)'A'))
            {
                return MESSAGEGPGGA;
            }
            else if ((Rx_BufferBufferd[index =1 ] == (char)'G') && (Rx_BufferBufferd[++index] == (char)'P') && (Rx_BufferBufferd[++index] == (char)'G') &&
                (Rx_BufferBufferd[++index] == (char)'S') && (Rx_BufferBufferd[++index] == (char)'A'))
            {
                return MESSAGEGPGSA;
            }
            else
            {
                return -1;
            }
        }

        /// <summary> Decoding ASCII characters. </summary>
        /// <returns> the value of the ASCII character </returns>
        private UInt16 ASCII_to_byte(char character)
        { //https://www.google.nl/search?client=opera&q=convert+c%23+decimal+to+char&sourceid=opera&ie=UTF-8&oe=UTF-8#q=c%23+convert+char+to+ascii+decimal
            if ((character >= '0') && (character <= '9')) //((character >= 48) && (character <= 57))
                return (byte)(character - '0');
            else if ((character >= 'A') && (character <= 'Z')) 
                return (byte)(character - 'A');
            else if ((character >= 'a') && (character <= 'z'))
                return (byte)(character - 'a');
            else // example: ascii char '0' = 48 dec. return just 0.
                return 0;
        }

        ////itoa = for ascii to decimal. ONLY IN C.
        private void Decodemessage_GPRMC()
        {
            // format: $GPRMC,200715.000,A,5012.6991,N,00711.9549,E,0.00,187.10,270115,,,A*65
            DateTime dateNow = DateTime.Now;
            if ( Rx_BufferBufferd[18] == 'A' ) // A meens data valid
            {
                int index = 6, tmp, hour, minutes, seconds, dotmilseconds;


                tmp = ASCII_to_byte(Rx_BufferBufferd[++index]);
                hour = (tmp * 10);
                hour += ASCII_to_byte(Rx_BufferBufferd[++index]);


                tmp = ASCII_to_byte(Rx_BufferBufferd[++index]);
                minutes = (tmp * 10);
                minutes += ASCII_to_byte(Rx_BufferBufferd[++index]);
                
                tmp = ASCII_to_byte(Rx_BufferBufferd[++index]);
                seconds = tmp * 10;
                seconds += ASCII_to_byte(Rx_BufferBufferd[++index]);
                
                index++;
                // dotmilisec is not implemented in the gps receiver
                tmp = ASCII_to_byte(Rx_BufferBufferd[++index]);
                dotmilseconds = tmp * 100;
                tmp = ASCII_to_byte(Rx_BufferBufferd[++index]);
                dotmilseconds += tmp * 10;
                dotmilseconds += ASCII_to_byte(Rx_BufferBufferd[++index]);

                DateTime date = new DateTime(dateNow.Year, dateNow.Month, dateNow.Day, hour, minutes, seconds);
                SetControlPropertyThreadSafe(lbl_gpstime, "Text", "GPS time: " + date.TimeOfDay.ToString());
            }
        }

        private void Decodemessage_GPGGA()
        {
            ;
        }

        private void Decodemessage_GPGSA()
        {
            ;
        }
        
    



        #region thread-safe call
        /*   To make a thread-safe call a Windows Forms control:

        1.  Query the control's InvokeRequired property.
        2.  If InvokeRequired returns true,  call Invoke with a delegate that makes the actual call to the control.
        3.  If InvokeRequired returns false, call the control directly.

        In the following code example, this logic is implemented in a utility method called SetText. 
        A delegate type named SetTextDelegate encapsulates the SetText method. 
        When the TextBox control's InvokeRequired returns true, the SetText method creates an instance
        of SetTextDelegate and calls the form's Invoke method. 
        This causes the SetText method to be called on the thread that created the TextBox control,
        and in this thread context the Text property is set directly

        also see: http://msdn2.microsoft.com/en-us/library/ms171728(VS.80).aspx
        */

        // This method demonstrates a pattern for making thread-safe calls on a Windows Forms control.
        // 
        // If the calling thread is different from the thread that created the TextBox control, this
        // method creates a SetTextCallback and calls itself asynchronously using the Invoke method.
        // 
        // If the calling thread is the same as the thread that created the TextBox control, the
        // Text property is set directly.
        #endregion   

        private void SetText(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.txtIn.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { text });
            }
            else this.txtIn.Text += text;
        }

        private void SetText_decoded(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.text_Decoded.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText_decoded);
                this.Invoke(d, new object[] { text });
            }
            else this.text_Decoded.Text += text;
        }

        private static StringBuilder receiveBuffer = new StringBuilder();
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            //SerialPort spL = (SerialPort) sender;
            int bufSize = 80;
            Byte[] dataBuffer = new Byte[bufSize];
            Console.WriteLine("Data Received at" + DateTime.Now);
            Console.WriteLine(port.Read(dataBuffer, 0, bufSize));
            //Console.WriteLine(spL.Read(dataBuffer, 0, bufSize));
            string s = System.Text.ASCIIEncoding.ASCII.GetString(dataBuffer);
            //here's the difference; append what you have to the buffer, then check it front-to-back
            //for known patterns indicating fields
            receiveBuffer.Append(s);

            //var regex = new Regex(@"(ID:\d*? State:\w{2} Zip:\d{5} StreetType:\w*? )");
            var regex = new Regex(@"($GPGGA)");

            Match match;
            do
            {
                match = regex.Match(receiveBuffer.ToString());
                if (match.Success)
                {
                    //"Process" the significant chunk of data
                    Console.WriteLine(match.Captures[0].Value);

                    //remove what we've processed from the StringBuilder.
                    receiveBuffer.Remove(match.Captures[0].Index, match.Captures[0].Length);
                }
            } while (match.Success);
        }
       
        private delegate void SetControlPropertyThreadSafeDelegate(Control control, string propertyName, object propertyValue);
        /// <summary> From another thread updating UI. input: textname, what to edit (text) and the new text.. </summary>
        public static void SetControlPropertyThreadSafe(Control control, string propertyName, object propertyValue)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new SetControlPropertyThreadSafeDelegate(SetControlPropertyThreadSafe), new object[] { control, propertyName, propertyValue });
            }
            else
            {
                control.GetType().InvokeMember(propertyName, BindingFlags.SetProperty, null, control, new object[] { propertyValue });
            }
        }

        private void timer_1sec_Tick(object sender, EventArgs e)
        {
            SetControlPropertyThreadSafe(lbl_systime, "Text", "System time: " + DateTime.Now.ToString("HH:mm:ss"));
        }


    }
}
