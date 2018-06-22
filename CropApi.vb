
Imports System.Net.Http
Imports ALTTPRCropDashboard.Data
Public Class CropApi
    Inherits ApiBase

    Public Function GetCrops() As IEnumerable(Of RunnerInfo)
        Dim response = Client.GetAsync("v1/crops").Result
        If response.IsSuccessStatusCode Then
            Return response.Content.ReadAsAsync(Of IEnumerable(Of RunnerInfo))({Formatter}).Result
        End If

        Return Nothing
    End Function

    Public Sub CreateCrop(crop As RunnerCropAdd)
        Dim response = Client.PostAsync("v1/crops", crop, Formatter).Result
        response.EnsureSuccessStatusCode()
        Dim returnData = response.Content.ReadAsAsync(Of RunnerCropAdd)({Formatter}).Result

        crop.SubmittedOn = returnData.SubmittedOn
        crop.Id = returnData.Id

    End Sub
    Public Sub UpdateCrop(crop As RunnerCropAdd)
        Dim response = Client.PutAsync($"v1/crops/{crop.Id}", crop, Formatter).Result
        response.EnsureSuccessStatusCode()


        Dim returnData = response.Content.ReadAsAsync(Of RunnerCropAdd)({Formatter}).Result
        crop.SubmittedOn = returnData.SubmittedOn
    End Sub

    Public Sub New(apiPath As String)
        MyBase.New(apiPath)
    End Sub

End Class