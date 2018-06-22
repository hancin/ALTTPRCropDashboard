
Imports System.Net.Http
Imports System.Net.Http.Formatting
Imports System.Net.Http.Headers
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Serialization

Public MustInherit Class ApiBase
    Protected Property Client As New HttpClient
    Protected Property Formatter As New JsonMediaTypeFormatter With {
        .SerializerSettings = New JsonSerializerSettings With {.Formatting = Formatting.Indented, .ContractResolver = New CamelCasePropertyNamesContractResolver}
    }

    Public Sub New(apiPath As String)
        Client.BaseAddress = New Uri(apiPath)
        Client.DefaultRequestHeaders.Accept.Clear()
        Client.DefaultRequestHeaders.Accept.Add(
            New MediaTypeWithQualityHeaderValue("application/json"))
        Client.Timeout = TimeSpan.FromSeconds(10)

        'Some apis are less well built than others...
        Formatter.SupportedMediaTypes.Add(New MediaTypeHeaderValue("text/html"))
    End Sub
End Class
