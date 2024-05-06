Imports System.Configuration
Imports System.IO
Imports System.Net
Imports System.Reflection
Imports DotRas
Imports Microsoft.Win32
Imports Tesseract

Public Class MainForm
    Dim RasCon As RasConnection
    Public Const Server As String = "https://www.vpnbook.com"
    Dim PassImagePath As String
    Public Server_htmltext As String
    Dim Password As String
    Dim AutoStart As Boolean = False
    Dim Connected As Boolean = False
    Dim TessDataPath As String '  Dim PassImagePath As String = Path.GetTempPath() + "pass.png"

    ' TODO:
    ' XX1: Still visible, maybe thread
    ' XX2: Check here if a connection could be established, if not, the form should remain visible.
    ' XX3: Better check if already connected
    'XX4: Try required?

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Initialize()


        Try
            If RasConnection.GetActiveConnections.Count >= 1 Then  ' Already Connected.    XX3
                LogEntry("Already Connected!")
                LoadSettings()
                Me.Hide() ' Fix | XX1
            Else
                If LoadSettings() = True Then
                    If CheckBox2.Checked Then
                        If txtIP.Text <> Nothing Then
                            Connect()
                            Me.Hide() ' | XX2
                        End If
                    Else
                        ShowingUp()
                    End If
                Else
                    txtUsername.Text = "vpnbook"
                    LogEntry("No Settings Found!")
                    If File.Exists(PassImagePath) = True Then
                        Try
                            My.Computer.FileSystem.DeleteFile(PassImagePath)
                        Catch ex As Exception
                            LogEntry("Could not delete old PasswordImage")
                        End Try
                    End If
                    DownloadAndProcessData()
                    ShowingUp()
                End If

            End If
        Catch ex As Exception
            LogEntry("Error:" + "No Permission to Access downloaded File!")
            ShowingUp()
        End Try
    End Sub


    Sub LogEntry(ByVal Text As String)
        If Text.Length > 3 Then
            If ListView1.InvokeRequired Then
                ListView1.Invoke(Sub() LogEntry(Text))

            Else
                Dim li As ListViewItem = ListView1.Items.Add(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"))
                li.SubItems.Add(Text)
            End If

        End If
    End Sub
    Sub ShowingUp()
        Me.Show()
        Me.ShowIcon = True
        Me.ShowInTaskbar = True

        BringToFront()
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        If Connected = True Then
            Disconnect()
        Else
            Connect()
        End If

    End Sub

    Private Sub CheckBox1_Changed(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged
        Try
            ToggleStartup(CheckBox1.Checked)
            My.Settings.AutoStart = CheckBox1.Checked
            My.Settings.Save()
        Catch ex As Exception
        End Try
    End Sub
    Private Sub CheckBox2_Changed(sender As Object, e As EventArgs) Handles CheckBox2.CheckedChanged
        Try
            My.Settings.AutoConnect = CheckBox2.Checked

            My.Settings.Save()
        Catch ex As Exception
        End Try
    End Sub
    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) _
     Handles Me.FormClosing
        e.Cancel = True
        Me.Hide()
        Me.ShowIcon = False
        Me.ShowInTaskbar = False

    End Sub
    Private Sub dialer_StateChanged(sender As Object, e As StateChangedEventArgs) Handles dialer.StateChanged
        Try
            If e.State = RasConnectionState.Connected Then
                LogEntry("Dialer: Connected")
                ConnectionStatus(True)
            ElseIf e.State = RasConnectionState.Disconnected Then
                LogEntry("Dialer: Disconnected")
                ConnectionStatus(False)
            End If
        Catch ex As Exception
            LogEntry(ex.ToString)
        End Try

    End Sub

    Private Sub dialer_DialCompleted(sender As Object, e As DialCompletedEventArgs) Handles dialer.DialCompleted  'XX3
        Try
            If e.Cancelled Then
                LogEntry("Connection canceled.")
            ElseIf e.TimedOut Then
                LogEntry("Connection timed out.")
            ElseIf e.Error IsNot Nothing Then
                LogEntry($"Connection error: {e.Error.Message}")
            ElseIf e.Connected Then
                LogEntry("Connection successful.")
                Connected = True
            Else
                Connected = False
            End If
        Catch ex As Exception
            LogEntry(ex.ToString)
        End Try

    End Sub
    Private Sub dialer_Error(sender As Object, e As System.IO.ErrorEventArgs) Handles dialer.Error
        LogEntry($"Dialer error: {e.GetException().Message}")

    End Sub


    Private Sub ListBoxSelected(sender As Object, e As EventArgs) Handles ListBox1.SelectedValueChanged
        txtIP.Text = ListBox1.GetItemText(ListBox1.SelectedItem)
    End Sub
#Region "Connection"

    Public Sub dialVPN(ByVal entryName As String, ByVal bookPath As String, ByVal credentials As Net.NetworkCredential)
        Try
            If dialer Is Nothing Then
                dialer = New RasDialer()
            End If
            dialer.EntryName = entryName
            dialer.PhoneBookPath = bookPath
            dialer.Credentials = credentials
            dialer.DialAsync()
        Catch ex As Exception
            LogEntry("Dialer Error: " + ex.ToString)
        End Try
    End Sub

    Sub Connect()
        Dim pptpEntry As RasEntry
        Dim creds As Net.NetworkCredential
        If txtIP.Text <> "" Then
            Try

                RasPhoneBook1.Open(True)
                If txtPassword.Text = "" Then
                    LogEntry("Can't Connect without Password!")
                Else

                    creds = New Net.NetworkCredential(txtUsername.Text, txtPassword.Text)
                    For Each RasEntry In RasPhoneBook1.Entries.ToList
                        If RasEntry.Name = txtIP.Text Then
                            dialVPN(txtIP.Text, RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User), creds)
                            Exit Sub
                        End If
                    Next
                    pptpEntry = RasEntry.CreateVpnEntry(txtIP.Text, txtIP.Text, RasVpnStrategy.PptpOnly, RasDevice.GetDeviceByName("(PPTP)", RasDeviceType.Vpn, False))
                    RasPhoneBook1.Entries.Add(pptpEntry) '
                    dialVPN(txtIP.Text, RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User), creds)
                End If

            Catch ex As Exception
                ShowingUp()
                LogEntry("Connection Error: " + ex.Message)


                CheckConnection()

            End Try
        Else
            LogEntry("Please Select a Server")
        End If
    End Sub

    Sub Disconnect()
        Try
            Try
                dialer.Dispose()
            Catch ex As Exception
            End Try
            Try
                RasCon = RasConnection.GetActiveConnectionByName(txtIP.Text, RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User))

            Catch ex As Exception
            End Try

            RasCon.HangUp() 'Ends the current VPN connection
            ConnectionStatus(False)
            LogEntry("VPN Disconnected")
        Catch ex As Exception
            LogEntry(ex.ToString)
        End Try
    End Sub
    Sub ConnectionStatus(ByVal Status As Boolean)
        Try
            If Me.InvokeRequired Then
                Me.Invoke(Sub() ConnectionStatus(Status))
            Else
                If Status = True Then
                    Connected = True
                    Button1.Invoke(Sub() Button1.Text = "Disconnect")
                    Me.Invoke(Sub() mnuConnect.Text = "Disconnect")
                    Me.Hide()
                    Me.ShowIcon = False
                    Me.ShowInTaskbar = False
                    SaveSettings()
                Else
                    Connected = False
                    Button1.Invoke(Sub() Button1.Text = "Connect")
                    Me.Invoke(Sub() mnuConnect.Text = "Connect")
                End If
            End If
        Catch ex As Exception
            'XXX4
        End Try
    End Sub
#End Region




#Region "Download&Interpretieren"
    Private Sub DownloadAndProcessData()
        Button3.Enabled = False
        Try
            LogEntry("DownloadHtml Data of: " + Server)
            Server_htmltext = WebData.DownloadHtml(Server)
            If Server_htmltext.Length > 10 Then

                LogEntry("Extracting Serverlist")
                GetServerList()

                LogEntry("Download PasswordImage")

                GetPassword()
                SaveSettings()

            Else
                LogEntry("Download failed!")


            End If
        Catch ex As Exception
            CheckConnection()

        End Try
        Button3.Enabled = True
    End Sub
    Sub GetServerList()
        Try
            Dim serverNames As List(Of String) = ExtractServerNames(Server_htmltext)
            ' Output the extracted server names
            For Each serverName In serverNames
                ListBox1.Items.Add(serverName)
            Next
        Catch ex As Exception
            LogEntry("Error: Can't extract Serverlist from htmltext Server: " + Server + vbNewLine + "More Information: " + ErrorToString())
        End Try
    End Sub
    Private WithEvents webClient As New WebClient()
    Sub GetPassword()
        If File.Exists(PassImagePath) = True Then
            Try
                File.Delete(PassImagePath) 'Lösche altes Image
            Catch ex As Exception
                LogEntry("Can't delete old PassImage")
                LogEntry(ex.ToString)
            End Try
        End If

        Try
            Dim PassURL As String = ExtractUrlWithRandomNumbers(Server_htmltext)
            webClient.DownloadFile(New Uri(PassURL), PassImagePath)
            LogEntry("Image Downloaded!")
            ExtractPassword()
        Catch ex As Exception
            LogEntry("Image Download Failed!")
            LogEntry(ex.ToString)
        End Try
    End Sub
    Private Sub ExtractPassword()
        LogEntry("Extracting Password.")
        Try
            Using engine As New TesseractEngine(Application.StartupPath + "\tessdata", "eng", EngineMode.TesseractOnly) '"./tessdata" OLD: Path.GetFullPath("./tessdata")
                engine.DefaultPageSegMode = PageSegMode.SingleBlock
                Using img As Pix = Pix.LoadFromFile(PassImagePath)
                    Using page As Page = engine.Process(img)
                        Dim text As String = page.GetText()
                        'Extracted Password
                        txtPassword.Text = text.TrimEnd

                    End Using
                End Using
            End Using

        Catch ex As Exception
            LogEntry("Can't extract Password!")
        End Try
    End Sub
#End Region





#Region "Settings & Startup"
    Sub SaveSettings()
        Try
            My.Settings.Server = txtIP.Text
            My.Settings.Username = txtUsername.Text
            My.Settings.Password = txtPassword.Text
            My.Settings.AutoStart = CheckBox1.Checked
            My.Settings.AutoConnect = CheckBox2.Checked
            Try
                For Each item In ListBox1.Items
                    If item <> Nothing Then
                        If Not My.Settings.ServerList.Contains(item) Then
                            My.Settings.ServerList.Add(item)
                        End If
                    End If

                Next
            Catch ex As Exception
                LogEntry(ex.ToString) 'REMOVEME
            End Try

            My.Settings.Save()
        Catch ex As Exception
            LogEntry("Failed to save Settings!")
        End Try
    End Sub
    Private Function LoadSettings() As Boolean
        Try
            If My.Settings.Password <> Nothing Then
                txtIP.Text = My.Settings.Server
                txtUsername.Text = My.Settings.Username
                txtPassword.Text = My.Settings.Password
                CheckBox1.Checked = My.Settings.AutoStart
                CheckBox2.Checked = My.Settings.AutoConnect
                Try
                    For Each item In My.Settings.ServerList
                        ListBox1.Items.Add(item)
                    Next
                Catch ex As Exception
                    LogEntry(ex.ToString)
                End Try

                Return True
            Else
                Return False
            End If
        Catch ex As Exception
            Return False
        End Try
    End Function
    Private Sub ToggleStartup(ByVal addToStartup As Boolean)
        Dim keyName As String = "Software\Microsoft\Windows\CurrentVersion\Run"
        Dim appName As String = "LoginMate-VPN"

        Try
            Using key As RegistryKey = Registry.CurrentUser.OpenSubKey(keyName, True)
                If key Is Nothing Then
                    LogEntry("Registry key not found.")
                    Return
                End If

                If addToStartup Then
                    ' Überprüfen, ob der Eintrag bereits existiert, bevor er hinzugefügt wird
                    If key.GetValue(appName) Is Nothing Then
                        key.SetValue(appName, Application.ExecutablePath)
                        LogEntry("The app has been added to startup.")
                    End If
                Else
                    ' Überprüfen, ob der Eintrag vorhanden ist, bevor er entfernt wird
                    If key.GetValue(appName) IsNot Nothing Then
                        key.DeleteValue(appName, False)
                        LogEntry("The app has been removed from startup.")
                    End If
                End If
            End Using
        Catch ex As UnauthorizedAccessException
            LogEntry("Access Violation. No Rights!")
        Catch ex As Exception
            LogEntry("Failed to change Startup!")
        End Try
    End Sub

#End Region

#Region "NoConnection"
    Public Sub CheckConnection()
        LogEntry("Checking Internet.")

        Try
            My.Computer.Network.Ping("8.8.8.8")
            LogEntry("Internet avaible.")
        Catch ex As Exception
            LogEntry("No Internet!")

            NoConnection()
        End Try
    End Sub
    Sub NoConnection()
        Task.Run(Async Function()
                     Do Until Await HasInternetConnectionAsync()
                         Await Wait2Async()
                     Loop
                 End Function)
    End Sub

    Private Async Function Wait2Async() As Task
        Await Task.Delay(2000)
    End Function

    Private Async Function HasInternetConnectionAsync() As Task(Of Boolean)
        Try
            Dim pingTask = Task.Run(Function() My.Computer.Network.Ping("8.8.8.8"))
            Return Await pingTask
        Catch ex As Exception
            Return False
        End Try
    End Function


    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        ListBox1.Items.Clear()
        txtPassword.Text = ""
        DownloadAndProcessData()

    End Sub
#End Region

#Region "TrayIcon"


    Private WithEvents Tray As NotifyIcon
    Private WithEvents MainMenu As ContextMenuStrip
    Private WithEvents mnuDisplayForm As ToolStripMenuItem
    Private WithEvents mnuSep1 As ToolStripSeparator
    Private WithEvents mnuConnect As ToolStripMenuItem
    Private WithEvents mnuExit As ToolStripMenuItem




    Public Sub Initialize()
        Try
            TessDataPath = (Application.StartupPath + "\tessdata\eng.traineddata")
            PassImagePath = Path.GetTempPath() + "pass.png"

            If File.Exists(TessDataPath) = False Then
                MsgBox("Error: Missing File: " + TessDataPath)
                Environment.Exit(2)
            End If





            RasPhoneBook1 = New RasPhoneBook()
            RasPhoneBook1.Open(True)



            'Initialize the menus
            mnuDisplayForm = New ToolStripMenuItem("Show Menu")
            mnuSep1 = New ToolStripSeparator()
            mnuExit = New ToolStripMenuItem("Exit")
            mnuConnect = New ToolStripMenuItem("Connect")

            MainMenu = New ContextMenuStrip
            MainMenu.Items.AddRange(New ToolStripItem() {mnuDisplayForm, mnuSep1, mnuConnect, mnuExit})

            'Initialize the tray
            Tray = New NotifyIcon
            Tray.Icon = My.Resources.ClientIcon

            Tray.ContextMenuStrip = MainMenu
            Tray.Text = "LoginMate-VPN"

            'Display
            Tray.Visible = True
            My.Settings.ServerList = New System.Collections.Specialized.StringCollection()
        Catch ex As Exception
            MsgBox("Error: " + ex.ToString)
        End Try
    End Sub



#Region "TrayIcon Events"
    Private Sub mnuConnect_Click(ByVal sender As Object, ByVal e As System.EventArgs) _
    Handles mnuConnect.Click
        ' Hier Connect Verbindung
        If Connected = True Then
            Disconnect()
        Else
            Connect()
        End If
    End Sub


    Private Sub mnuDisplayForm_Click(ByVal sender As Object, ByVal e As System.EventArgs) _
    Handles mnuDisplayForm.Click
        ShowingUp()
    End Sub

    Private Sub mnuExit_Click(ByVal sender As Object, ByVal e As System.EventArgs) _
    Handles mnuExit.Click
        Environment.Exit(0)

    End Sub

    Private Sub Tray_DoubleClick(ByVal sender As Object, ByVal e As System.EventArgs) _
    Handles Tray.DoubleClick
        MainMenu.Show()
    End Sub

    Private Sub LinkLabel1_LinkClicked(ByVal sender As System.Object, ByVal e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel1.LinkClicked
        System.Diagnostics.Process.Start(Server)
    End Sub





#End Region
#End Region

End Class


