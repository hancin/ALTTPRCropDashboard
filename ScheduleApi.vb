Imports System.Net.Http
Imports ALTTPRCropDashboard.Data.Schedule

Public Class ScheduleApi
    Inherits ApiBase
    Public Sub New(urlBase As String)
        MyBase.New(urlBase)
    End Sub

    Public Function LoadEpisode(id As Integer) As ScheduleEvent

        Dim response = Client.GetAsync("episode?id=" & id).Result
        If response.IsSuccessStatusCode Then
            Return response.Content.ReadAsAsync(Of ScheduleEvent)({Formatter}).Result
        End If

        Return Nothing
    End Function
End Class
