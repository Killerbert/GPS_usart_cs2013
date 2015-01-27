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
        public const int MaxLengthMessage = 100;
        public const int MinLengthMessage = 10;
        public const int Newstartframefound = 0;
        public const int Nostartfound = 1;
        public const int Completeframefound = 2;
        public const int Incompleteframefound = 4;
        
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




        char[] Rx_Buffer = new char[MaxLengthMessage];
        char[] Rx_BufferBufferd = new char[MaxLengthMessage];
        
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
                case Newstartframefound:
                    Rx_Buffer[0] = Rx_Buffer[buffindex];
                    buffindex = 0;
                    break;
                case Nostartfound:
                    break;
                case Incompleteframefound:
                    break;
                case Completeframefound:
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
            
            if (buffindex + 1 < MaxLengthMessage) 
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
                return Newstartframefound;
            }
            else if (Rx_Buffer[0] != (char)'$')
            {
                return Nostartfound; 
            }
            // wel begin
            else if (Rx_Buffer[buffindex] == (char)'\n') /* next line windows: \r \n */
            {
                return Completeframefound; // compleet frame gevonden
            }
            else
                return Incompleteframefound;
        }


         private  void Newframefound()
        {
            //https://msdn.microsoft.com/en-us/library/zycewsya.aspx
           // if (Rx_BufferBufferd[] == "$GPRMC") 
            {
                int a = 10;
            }

            //~ uint8_t *cmdPtr = datatosend;
            //~ *cmdPtr++ = XBEE_STARTDELIMITER;
            //~ uint8_t *lengthPtr = cmdPtr;
            //~ cmdPtr += 2;//length
            //~ *cmdPtr++ = frameIDnr_incr();
            //~ memcpy(cmdPtr, strAPI.IPv4_sourceIP, sizeof(strAPI.IPv4_sourceIP));
            //~ cmdPtr += sizeof(strAPI.IPv4_sourceIP);	
            //~ *lengthPtr = cmdPtr - datatosend;
            
         

        }



        /// <summary>
        /// Original
        /// </summary>
        private void port_DataReceived_1(object sender, SerialDataReceivedEventArgs e)
        {
            InputData = port.ReadExisting();
            if (InputData != String.Empty)
            {
              //  txtIn.Text = InputData;   // because of different threads this does not work properly !!
                SetText(InputData);
            }
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

    }
}
