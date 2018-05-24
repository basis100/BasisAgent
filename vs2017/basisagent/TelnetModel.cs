using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Net.Security;
#if EMULATION
using Dart.Emulation;
#elif TELNET
using Dart.Telnet;
#endif
using Dart.Telnet;

public enum SecurityType { None, Explicit, Implicit };

/// <summary>
/// This class encapsulates the "Model" part of the Model-View-Controller design pattern, and is used by samples that 
/// utilize the PowerTCP Telnet component (part of the Emulation for .NET and Telnet for .NET products).  This class
/// can be added to additional applications without the need for cut-and-paste.  Note that because this class is used 
/// in both the Emulation and Telnet product samples, a compile-time directive indicates which namespace to use
/// (Dart.Emulation or Dart.Telnet).
/// </summary>
/// <remarks>
/// This class is marked with the Serializable attribute so values can be easily stored, and restored later.
/// </remarks>
[Serializable]
public class TelnetModel
{
    [NonSerialized]
    public Telnet Telnet;
    private TcpSession session;
    private Credentials credentials;
    private ClientSecurity security;
    private string commandString;
    private bool receiveLoopRequired;
    private SecurityType securityType;
    private bool localEcho;
    private bool useCrLf;

    public TelnetModel()
    {
        this.session = new TcpSession();
        this.credentials = new Credentials();
        this.security = new ClientSecurity();
        this.security.ValidationCallback = this.RemoteCertificateValidation;
        this.security.SelectionCallback = this.LocalCertificateValidation;
        this.commandString = "";
        this.receiveLoopRequired = true;
        this.securityType = SecurityType.None;
        this.useCrLf = true;
        this.localEcho = false;
    }

    public TcpSession Session
    {
        get { return this.session; }
    }

    public Credentials Credentials
    {
        get { return this.credentials; }
    }

    public ClientSecurity Security
    {
        get { return this.security; }
    }

    public string CommandString
    {
        get { return this.commandString; }
        set { this.commandString = value; }
    }

    public bool ReceiveLoopRequired
    {
        get { return this.receiveLoopRequired; }
        set { this.receiveLoopRequired = value; }
    }

    public SecurityType SecurityType
    {
        get { return this.securityType; }
        set
        {
            this.securityType = value;
            this.security.Protocols = (this.securityType == SecurityType.None)
                ? SslProtocols.None : SslProtocols.Default;
        }
    }

    public bool UseCrLf
    {
        get { return this.useCrLf; }
        set { this.useCrLf = value; }
    }

    public bool LocalEcho
    {
        get { return this.localEcho; }
        set { this.localEcho = value; }
    }

    /// <summary>
    /// Connects to the remote server and starts receiving data to write to the display.
    /// </summary>
    public void Connect(object state)
    {
        //Necessary for explicit security
        EventHandler<CommandEventArgs> commandReceivedHandler =
            new EventHandler<CommandEventArgs>(telnet_CommandReceived);

        try
        {
            //Connect to the server
            Telnet.Connect(session);
            Telnet.Socket.ReceiveTimeout = 5000;

            //If Explicit security used, use the CommandReceived event to negotiate
            if (securityType == SecurityType.Explicit)
                Telnet.CommandReceived += commandReceivedHandler;
            //If Implicit security used, authenticate server and specify certificate callback functions
            else if (securityType == SecurityType.Implicit)
                Telnet.AuthenticateAsClient(security);

            //Login and provide transferred data to the interface (Data event)
            if (session.RemoteEndPoint.Port > 0 && credentials.Username.Length > 0 &&
                credentials.Password.Length > 0)
            {
                Data data = Telnet.Login(credentials);
                Telnet.Marshal(data, string.Empty, null);
            }

            //Provide telnet.Stream to interface (UserState event), or start the read loop.
            Telnet.Socket.ReceiveTimeout = 0;
            if (ReceiveLoopRequired) ReceiveData();
            else Telnet.Marshal("", Telnet.GetStream());
        }
        catch (Exception ex)
        {
            Telnet.Marshal(ex);
        }
        finally
        {
            Telnet.CommandReceived -= commandReceivedHandler;
        }
    }

    /// <summary>
    /// Receives data from the server for display
    /// </summary>
    public void ReceiveData()
    {
        //Receive data when it is sent by the remote host and marshal it to UI thread
        byte[] buffer = new byte[1024];
        Data data;
        while (IsConnected)
        {
            data = Telnet.Read(buffer, 0, 1024);
            if (data != null) Telnet.Marshal(data, string.Empty, null);
        }
    }

    public void WriteData(string data)
    {
        //Write data string
        if (IsConnected) Telnet.Write(data);
    }

    public void WriteData(byte[] data)
    {
        //Write data bytes
        if (IsConnected) Telnet.Write(data);
    }

    public void Execute(object state)
    {
        //Connect to the specified server
        receiveLoopRequired = false;
        Connect(session);

        //Execute command
        ExecuteCommand(CommandString, Credentials.CommandPrompt);

        //Close
        Telnet.Close();
    }

    public void ExecuteCommand(string command, string prompt)
    {
        //Write data and display response
        if (IsConnected)
        {
            Telnet.Socket.ReceiveTimeout = 5000;
            Telnet.Write(command + "\r\n");
            try
            {
                Telnet.Marshal(Telnet.ReadToDelimiter(prompt), string.Empty, null);
            }
            catch (DataException dEx)
            {
                Telnet.Marshal(dEx.DataRead, string.Empty, null);
                Telnet.Marshal(dEx);
            }
            finally
            {
                Telnet.Socket.ReceiveTimeout = 0;
            }
        }
    }

    public bool IsConnected
    {
        //True if connection is open
        get { return Telnet.State != ConnectionState.Closed; }
    }

    [field: NonSerialized]
    public event EventHandler<LocalCertificateEventArgs> CertificateRequested;

    [field: NonSerialized]
    public event EventHandler<RemoteCertificateEventArgs> CertificatePresented;

    public bool AcceptCertificate;

    private bool RemoteCertificateValidation(Object sender, X509Certificate remoteCertificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        CertificatePresented(this, new RemoteCertificateEventArgs(remoteCertificate, chain, sslPolicyErrors));
        return AcceptCertificate;
    }

    private X509Certificate LocalCertificateValidation(Object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
    {
        //This callback executes (on worker thread) if server requests a client certificate
        if (this.Security.Certificates.Count == 1)
            return this.Security.Certificates[0];
        else
        {
            //Prompt the user to select a certificate from the certificate list
            CertificateRequested(this, new LocalCertificateEventArgs(targetHost, localCertificates, remoteCertificate, acceptableIssuers));
            if (this.Security.Certificates.Count > 0) return this.Security.Certificates[0];
        }
        return null;
    }

    /// <summary>
    /// Handles negotiation for explicitly secure connection
    /// </summary>
    private void telnet_CommandReceived(object sender, CommandEventArgs e)
    {
        try
        {
            if (e.OptionCode == OptionCode.Authentication)
            {
                if (e.Command == Command.Do)
                    Telnet.SendOption(Command.Will, OptionCode.Authentication);
                else if (e.Command == Command.SB && e.SubOption[0] == 1)
                {
                    //Server expects a SEND request (IS)
                    byte[] response = new byte[4];
                    response[0] = 0; // is
                    response[1] = 7; // ssl
                    response[2] = 0; // AuthClientToServer
                    response[3] = 1; // START_SSL is our request
                    Telnet.SendSubOption(e.OptionCode, response);
                }
                else if (e.Command == Command.SB && e.SubOption[0] == 2)
                    Telnet.AuthenticateAsClient(security);
            }
        }
        catch (Exception ex)
        {
            Telnet.Close();
            Telnet.Marshal(ex);
        }
    }
}

/// <summary>
/// Arguments for the RemoteCertificateValidation event this class raises when a server presents its certificate.
/// </summary>
public class RemoteCertificateEventArgs : System.EventArgs
{
    public RemoteCertificateEventArgs(X509Certificate remoteCertificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        RemoteCertificate = remoteCertificate;
        Chain = chain;
        SslPolicyErrors = sslPolicyErrors;
    }

    public X509Certificate RemoteCertificate;
    public X509Chain Chain;
    public SslPolicyErrors SslPolicyErrors;
}

/// <summary>
/// Arguments for the LocalCertificateValidation event this class raises when a server requests a certificate.
/// </summary>
public class LocalCertificateEventArgs : System.EventArgs
{
    public LocalCertificateEventArgs(string targetHost, X509CertificateCollection localCertificates,
        X509Certificate remoteCertificate, string[] acceptableIssuers)
    {
        TargetHost = targetHost;
        LocalCertificates = localCertificates;
        RemoteCertificate = remoteCertificate;
        AcceptableIssuers = acceptableIssuers;
    }

    public string TargetHost;
    public X509CertificateCollection LocalCertificates;
    public X509Certificate RemoteCertificate;
    public string[] AcceptableIssuers;
}

