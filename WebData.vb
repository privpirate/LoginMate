Imports System.Net
Imports System.Net.Http
Imports System.Text.RegularExpressions
Imports HtmlAgilityPack

Module WebData
    Public Sub DownloadFileFromUrl(url As String, savePath As String)
        Try
            Using client As New WebClient()
                client.DownloadFile(url, savePath)  ' Herunterladen der Datei von der URL und speichern an dem angegebenen Speicherort
            End Using
        Catch ex As Exception
            MainForm.CheckConnection()
        End Try
    End Sub

    Function DownloadHtml(url As String) As String
        Try
            Using client As New HttpClient()
                ' Download HTML data
                Dim htmlBytes As Byte() = client.GetByteArrayAsync(url).Result

                ' Convert bytes to UTF-8
                Dim htmlText As String = System.Text.Encoding.UTF8.GetString(htmlBytes)

                Return htmlText
            End Using
        Catch ex As Exception
            MainForm.LogEntry("Error: Can't download htmltext of: " + MainForm.Server + vbNewLine + "More Information: " + ErrorToString())
            MainForm.CheckConnection()

        End Try

    End Function

    Function ExtractUrlWithRandomNumbers(htmlText As String) As String
        Dim regexPattern As String = "password\.php\?t=([^""]+)""\s*/></strong></li>"  ' Regulärer Ausdruck für den URL-String mit den zufälligen Zahlen
        Dim match As Match = Regex.Match(htmlText, regexPattern)    ' Anwendung des regulären Ausdrucks auf den HTML-Text
        If match.Success Then       ' Überprüfung, ob ein Treffer gefunden wurde
            Dim fullMatch As String = match.Groups(0).Value        ' Der vollständige Treffer (inklusive "password.php?t=")
            Dim randomNumberPart As String = match.Groups(1).Value      ' Extraktion der Zahlen
            Dim extractedUrl As String = $"https://www.vpnbook.com/password.php?t={randomNumberPart}"    ' Zusammenfügen der extrahierten Zahlen zu einem URL-String // Mit der Server-konstante für weniger Text ersetzen
            Return extractedUrl
        Else
            Return String.Empty   ' Kein Treffer gefunden
        End If
    End Function


    Function ExtractServerNames(htmlText As String) As List(Of String)
        Dim serverNames As New List(Of String)()

        ' Create HtmlAgilityPack document
        Dim htmlDocument As New HtmlDocument()
        htmlDocument.LoadHtml(htmlText)

        ' XPath expression for the server names
        Dim xpathExpression As String = "//li/strong/span[@class='red']/following-sibling::text()"

        ' Select server names with XPath
        Dim serverNodes As HtmlNodeCollection = htmlDocument.DocumentNode.SelectNodes(xpathExpression)

        ' Add server names to the list
        If serverNodes IsNot Nothing Then
            For Each serverNode In serverNodes
                ' Check if the text is not empty or whitespace
                If Not String.IsNullOrWhiteSpace(serverNode.InnerHtml.Trim()) Then
                    serverNames.Add(serverNode.InnerHtml.Trim())
                End If
            Next
        End If

        Return serverNames

    End Function


End Module
