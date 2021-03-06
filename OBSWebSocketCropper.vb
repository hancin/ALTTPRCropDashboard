﻿Imports System.Configuration
Imports OBSWebsocketDotNet
Imports ALTTPRCropDashboard.Data
Imports ALTTPRCropDashboard.DB
Imports System.IO
Imports ALTTPRCropDashboard.Data.ViewModels
Imports System.Globalization

Public Class ObsWebSocketCropper
    Public ProgramName As String = "OBS WebSocket Cropper"

    Public WithEvents Obs As New ObsWebSocketPlus
    Public ObsConnectionStatus As String
    Public ObsConnectionStatus2 As String

    Private WithEvents _obs2 As New ObsWebSocketPlus
    Public ReadOnly Property ConnectionString As String
        Get
            Return My.Settings.ConnectionString1 & ":" & My.Settings.ConnectionPort1
        End Get
    End Property
    Public ReadOnly Property ConnectionString2 As String
        Get
            Return My.Settings.ConnectionString2 & ":" & My.Settings.ConnectionPort2
        End Get
    End Property

    Public Shared ObsSettingsResult As String
    Public Shared NewRunnerName As String
    Public Shared NewRunnerTwitch As String
    Public Shared GetObsInfo As Boolean
    Public Shared ReuseInfo As Boolean

    Private Const ApprovedChars As String = "0123456789"

    Private _cropApi As CropApi
    Private ReadOnly _cropperMath As New CropperMath
    Private _vlcListLeft As New DataSet
    Private _vlcListRight As New DataSet
    Private ReadOnly _viewModel As New CropperViewModel
    Private _check2NdObs As Boolean
    Private _lastUpdate As Integer

#Region " Create New Tables "
    Private Sub CreateNewSourceTable()
        If _vlcListLeft.Tables.Count = 0 Then
            _vlcListLeft.Tables.Add("Processes")
            _vlcListLeft.Tables("Processes").Columns.Add("VLCName")
        Else
            _vlcListLeft.Tables("Processes").Clear()
        End If

        If _vlcListRight.Tables.Count = 0 Then
            _vlcListRight.Tables.Add("Processes")
            _vlcListRight.Tables("Processes").Columns.Add("VLCName")
        Else
            _vlcListRight.Tables("Processes").Clear()
        End If

    End Sub

#End Region
#Region " Button Clicks "
    Private Sub btnSetRightCrop_Click(sender As Object, e As EventArgs) Handles btnSetRightCrop.Click
        If _viewModel.RightRunner.MasterSize.Height = 0 OrElse _viewModel.RightRunner.MasterSize.Width = 0 Then
            _viewModel.RightRunner.MasterSize.UpdateFromSize(GetMasterSize(True))
        End If
        Try
            SetNewNewMath(True)
        Catch ex As ErrorResponseException
            MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
        End Try
    End Sub
    Private Sub btnConnectOBS1_Click(sender As Object, e As EventArgs) Handles btnConnectOBS1.Click
        ConnectToObs()
    End Sub
    Private Sub btnGetLeftCrop_Click(sender As Object, e As EventArgs) Handles btnGetLeftCrop.Click
        Try
            If MsgBox("This action will overwrite the current crop info for all game/timer windows!  Are you sure you wish to continue?", MsgBoxStyle.YesNo, ProgramName) = MsgBoxResult.Yes Then
                FillCurrentCropInfoFromObs(False)
            End If
        Catch ex As ErrorResponseException
            MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
        End Try
    End Sub
    Private Sub btnGetRightCrop_Click(sender As Object, e As EventArgs) Handles btnGetRightCrop.Click
        Try
            If MsgBox("This action will overwrite the current crop info for all game/timer windows!  Are you sure you wish to continue?", MsgBoxStyle.YesNo, ProgramName) = MsgBoxResult.Yes Then
                FillCurrentCropInfoFromObs(True)
            End If
        Catch ex As ErrorResponseException
            MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
        End Try
    End Sub
    Private Sub btnSetLeftCrop_Click(sender As Object, e As EventArgs) Handles btnSetLeftCrop.Click
        Try
            If _viewModel.LeftRunner.MasterSize.Height = 0 OrElse _viewModel.LeftRunner.MasterSize.Width = 0 Then
                _viewModel.LeftRunner.MasterSize.UpdateFromSize(GetMasterSize(False))
            End If

            SetNewNewMath(False)
        Catch ex As ErrorResponseException
            MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
        End Try
    End Sub
    Private Sub btnSetTrackCommNames_Click(sender As Object, e As EventArgs) Handles btnSetTrackCommNames.Click
        If Not String.IsNullOrWhiteSpace(My.Settings.LeftRunnerOBS) AndAlso Not String.IsNullOrWhiteSpace(cbLeftRunnerName.Text) Then
            DispatchToObs(Sub(o) o.SetTextGdi(My.Settings.LeftRunnerOBS, cbLeftRunnerName.Text))
        End If
        If Not String.IsNullOrWhiteSpace(My.Settings.RightRunnerOBS) AndAlso Not String.IsNullOrWhiteSpace(cbRightRunnerName.Text) Then
            DispatchToObs(Sub(o) o.SetTextGdi(My.Settings.RightRunnerOBS, cbRightRunnerName.Text))
        End If
        If Not String.IsNullOrWhiteSpace(My.Settings.CommentaryOBS) AndAlso Not String.IsNullOrWhiteSpace(txtCommentaryNames.Text) Then
            DispatchToObs(Sub(o) o.SetTextGdi(My.Settings.CommentaryOBS, txtCommentaryNames.Text))
        End If
        If Not String.IsNullOrWhiteSpace(My.Settings.LeftTrackerOBS) AndAlso Not String.IsNullOrWhiteSpace(txtLeftTrackerURL.Text) Then
            Dim TrackerURL = If(ConfigurationManager.AppSettings("TrackerURL"), "")
            Dim trackerString As String
            If txtLeftTrackerURL.Text.ToLower.StartsWith("http") Then
                trackerString = txtLeftTrackerURL.Text
            Else
                trackerString = TrackerURL & txtLeftTrackerURL.Text
            End If

            DispatchToObs(Sub(o) o.SetBrowserSource(My.Settings.LeftTrackerOBS, trackerString))
        End If
        If Not String.IsNullOrWhiteSpace(My.Settings.RightTrackerOBS) AndAlso Not String.IsNullOrWhiteSpace(txtRightTrackerURL.Text) Then
            Dim TrackerURL = If(ConfigurationManager.AppSettings("TrackerURL"), "")
            Dim trackerString As String
            If txtRightTrackerURL.Text.ToLower.StartsWith("http") Then
                trackerString = txtRightTrackerURL.Text
            Else
                trackerString = TrackerURL & txtRightTrackerURL.Text
            End If

            DispatchToObs(Sub(o) o.SetBrowserSource(My.Settings.RightTrackerOBS, trackerString))
        End If



    End Sub

    Private Sub ObsConnectionChanged(sender As Object, e As EventArgs) Handles Obs.Connected, Obs.Disconnected
        RefreshVlc()
        ObsConnectionStatus = If(Obs.IsConnected, "Connected", "Not Connected")
        _viewModel.ObsConnected = Obs.IsConnected
        lblOBS1ConnectedStatus.Text = ObsConnectionStatus

        If Not _viewModel.ObsConnected Then
            'If our primary OBS connection died, then the secondary connection is also no longer useful.
            DispatchToObs(Sub(o) o.Disconnect())
        End If
    End Sub
    Private Sub Obs2ConnectionChanged(sender As Object, e As EventArgs) Handles _obs2.Connected, _obs2.Disconnected
        ObsConnectionStatus2 = If(_obs2.IsConnected, "Connected", "Not Connected")
        lblOBS2ConnectedStatus.Text = ObsConnectionStatus
    End Sub
    Private Sub btnSaveLeftCrop_Click(sender As Object, e As EventArgs) Handles btnSaveLeftCrop.Click
        SaveRunnerCrop(False)
    End Sub
    Private Sub btnSaveRightCrop_Click(sender As Object, e As EventArgs) Handles btnSaveRightCrop.Click
        SaveRunnerCrop(True)
    End Sub
    Private Sub btnSyncWithServer_Click(sender As Object, e As EventArgs) Handles btnSyncWithServer.Click
        SyncWithServer()
    End Sub
    Private Sub SyncWithServer()
        Cursor = Cursors.WaitCursor
        Try
            If Not String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings("ServerURL")) Then
                _cropApi = New CropApi(ConfigurationManager.AppSettings("ServerURL"))

                SendToServer()
                GetSyncFromServer()
            Else
                MsgBox("You are missing the API config file.  Please ask someone in the restream channel in discord if you believe you should need this file.", MsgBoxStyle.OkOnly, ProgramName)
            End If
        Finally
            Cursor = Cursors.Default
        End Try
    End Sub
    Private Sub btnGetProcesses_Click(sender As Object, e As EventArgs) Handles btnGetProcesses.Click
        RefreshVlc()
    End Sub
    Private Sub btnSetLeftVLC_Click(sender As Object, e As EventArgs) Handles btnSetLeftVLC.Click
        SetVlcWindows(False)
    End Sub
    ''' <summary>
    ''' Call the same code on all connected OBS instances
    ''' </summary>
    ''' <param name="callback">The code to execute</param>
    Private Sub DispatchToObs(callback As Action(Of ObsWebSocketPlus))
        If callback Is Nothing Then
            Exit Sub
        End If

        If Obs.IsConnected Then
            callback(Obs)
        End If
        If _obs2.IsConnected Then
            callback(_obs2)
        End If

    End Sub

    Private Sub SetVlcWindows(isRightWindow As Boolean)

        Dim vlcSource As String = If(isRightWindow, cbRightVLCSource.Text, cbLeftVLCSource.Text)
        Dim gameSource As String = If(isRightWindow, My.Settings.RightGameName, My.Settings.LeftGameName)
        Dim timerSource As String = If(isRightWindow, My.Settings.RightTimerName, My.Settings.LeftTimerName)
        Dim vmRunner = If(isRightWindow, _viewModel.RightRunner, _viewModel.LeftRunner)
        If Not String.IsNullOrWhiteSpace(vlcSource) Then
            vlcSource = vlcSource.Replace(":", "#3A") & ":QWidget:vlc.exe"
            If Not String.IsNullOrWhiteSpace(gameSource) Then
                DispatchToObs(Sub(o) o.SetSourceSettings(gameSource, False, vlcSource, 1))
            End If
            If Not String.IsNullOrWhiteSpace(timerSource) Then
                DispatchToObs(Sub(o) o.SetSourceSettings(timerSource, False, vlcSource, 1))
            End If

            GetCurrentCropSettings(isRightWindow)

        End If

    End Sub
    Private Sub btn2ndOBS_Click(sender As Object, e As EventArgs) Handles btn2ndOBS.Click
        GetIniFile(False, False)

        _check2NdObs = True
        Timer1.Start()


    End Sub
    Private Sub btnConnectOBS2_Click(sender As Object, e As EventArgs) Handles btnConnectOBS2.Click
        ConnectToObs2()
    End Sub
    Private Sub btnSetRightVLC_Click(sender As Object, e As EventArgs) Handles btnSetRightVLC.Click
        SetVlcWindows(True)
    End Sub
    Private Sub btnNewLeftRunner_Click(sender As Object, e As EventArgs) Handles btnNewLeftRunner.Click
        AddNewRunner(False)
    End Sub
    Private Sub btnNewRightRunner_Click(sender As Object, e As EventArgs) Handles btnNewRightRunner.Click
        AddNewRunner(True)
    End Sub
    Private Sub btnLeftTimerDB_Click(sender As Object, e As EventArgs) Handles btnLeftTimerDB.Click
        ClearTextBoxes(False, "Timer")
        RefreshCropFromData(False, "Timer")
    End Sub
    Private Sub btnLeftGameDB_Click(sender As Object, e As EventArgs) Handles btnLeftGameDB.Click
        ClearTextBoxes(False, "Game")
        RefreshCropFromData(False, "Game")
    End Sub
    Private Sub btnRightTimerDB_Click(sender As Object, e As EventArgs) Handles btnRightTimerDB.Click
        ClearTextBoxes(True, "Timer")
        RefreshCropFromData(True, "Timer")
    End Sub
    Private Sub btnRightGameDB_Click(sender As Object, e As EventArgs) Handles btnRightGameDB.Click
        ClearTextBoxes(True, "Game")
        RefreshCropFromData(True, "Game")
    End Sub
    Private Sub btnLeftTimerUncrop_Click(sender As Object, e As EventArgs) Handles btnLeftTimerUncrop.Click
        If Not String.IsNullOrWhiteSpace(My.Settings.LeftTimerName) Then
            Uncrop(My.Settings.LeftTimerName)
        End If
    End Sub
    Private Sub btnRightTimerUncrop_Click(sender As Object, e As EventArgs) Handles btnRightTimerUncrop.Click
        If Not String.IsNullOrWhiteSpace(My.Settings.RightTimerName) Then
            Uncrop(My.Settings.RightTimerName)
        End If
    End Sub
    Private Sub btnRightGameUncrop_Click(sender As Object, e As EventArgs) Handles btnRightGameUncrop.Click
        If Not String.IsNullOrWhiteSpace(My.Settings.RightGameName) Then
            Uncrop(My.Settings.RightGameName)
        End If
    End Sub
    Private Sub btnLeftGameUncrop_Click(sender As Object, e As EventArgs) Handles btnLeftGameUncrop.Click
        If Not String.IsNullOrWhiteSpace(My.Settings.LeftGameName) Then
            Uncrop(My.Settings.LeftGameName)
        End If
    End Sub
#End Region
#Region " Crop Math / Crop Settings "
    Private Sub GetCurrentCropSettings(isRightWindow As Boolean)
        Dim runnerVm = If(isRightWindow, _viewModel.RightRunner, _viewModel.LeftRunner)
        runnerVm.CurrentSize.UpdateFromSize(GetMasterSize(isRightWindow))

        SetHeightLabels()
    End Sub
    Private Sub SaveRunnerCrop(isRightWindow As Boolean)

        Dim needsRefresh = False
        Dim submitterName = My.Settings.TwitchChannel
        Dim runnerVm = If(isRightWindow, _viewModel.RightRunner, _viewModel.LeftRunner)

        Dim runnerTwitch = runnerVm.Twitch
        Dim runnerName = runnerVm.Name
        GetCurrentCropSettings(isRightWindow)

        Dim savedMasterSize = runnerVm.MasterSize.AsSize()
        Dim masterSizeWithoutDefaultRight As Size = _cropperMath.RemoveDefaultCropSize(_cropperMath.RemoveScaling(savedMasterSize, runnerVm.Scale))
        Dim cropWithoutDefaultGame As Rectangle = _cropperMath.RemoveDefaultCrop(_cropperMath.RemoveScaling(runnerVm.GameCrop.AsRectangle(), savedMasterSize, runnerVm.Scale))
        Dim cropWithoutDefaultTimer As Rectangle = _cropperMath.RemoveDefaultCrop(_cropperMath.RemoveScaling(runnerVm.TimerCrop.AsRectangle(), savedMasterSize, runnerVm.Scale))

        Using context As New CropDbContext
            If Not String.IsNullOrWhiteSpace(runnerTwitch) Then
                Dim runner = context.Crops.FirstOrDefault(Function(x) x.Submitter = submitterName AndAlso x.Runner = runnerTwitch)
                If runner Is Nothing Then
                    'Swap with twitch name
                    runner = New Crop With {
                        .Submitter = submitterName,
                        .Runner = runnerTwitch,
                        .Id = Guid.NewGuid()
                        }
                    context.Crops.Add(runner)
                    needsRefresh = True
                End If

                runner.GameCropTop = cropWithoutDefaultGame.Top
                runner.GameCropBottom = cropWithoutDefaultGame.Bottom
                runner.GameCropRight = cropWithoutDefaultGame.Right
                runner.GameCropLeft = cropWithoutDefaultGame.Left
                runner.TimerCropTop = cropWithoutDefaultTimer.Top
                runner.TimerCropBottom = cropWithoutDefaultTimer.Bottom
                runner.TimerCropRight = cropWithoutDefaultTimer.Right
                runner.TimerCropLeft = cropWithoutDefaultTimer.Left
                runner.SizeHeight = masterSizeWithoutDefaultRight.Height
                runner.SizeWidth = masterSizeWithoutDefaultRight.Width
                runner.SubmittedOn = Nothing
                runner.RunnerName = runnerName

                context.SaveChanges()
            End If
        End Using

        If needsRefresh Then
            RefreshRunnerNames()
        End If
    End Sub
    Private Function GetMasterSize(isRight As Boolean) As Size

        If isRight AndAlso String.IsNullOrWhiteSpace(My.Settings.RightGameName) Then
            Return Size.Empty
        End If
        If Not isRight AndAlso String.IsNullOrWhiteSpace(My.Settings.LeftGameName) Then
            Return Size.Empty
        End If

        Dim currentScene = If(Obs.StudioModeEnabled, Obs.GetPreviewScene(), Obs.GetCurrentScene())
        Dim target = If(isRight, My.Settings.RightGameName, My.Settings.LeftGameName).ToLower

        Dim adequateSource = currentScene.Items.FirstOrDefault(Function(scene) scene.SourceName.ToLower = target)

        If adequateSource.SourceName Is Nothing OrElse String.IsNullOrWhiteSpace(adequateSource.SourceName) Then
            MessageBox.Show(Me, $"Cannot find source {target} in the current scene. Are you on the right scene?")
        End If

        Return New Size(adequateSource.SourceWidth, adequateSource.SourceHeight)

    End Function
    Private Sub ResetHeightWidthLabels()
        lblLMasterHeight.Text = "Master Height:  0"
        lblLMasterWidth.Text = "Master Width: 0"
        lblLSourceHeight.Text = "Source Height: 0"
        lblLSourceWidth.Text = "Master Width: 0"

        lblRMasterHeight.Text = "Master Height: 0"
        lblRMasterWidth.Text = "Master Width: 0"
        lblRSourceHeight.Text = "Source Height: 0"
        lblRSourceWidth.Text = "Master Width: 0"
    End Sub
    Private Sub SetHeightLabels()
        lblLMasterHeight.Text = "Master Height: " & _viewModel.LeftRunner.MasterSize.Height
        lblLMasterWidth.Text = "Master Width: " & _viewModel.LeftRunner.MasterSize.Width
        lblLSourceHeight.Text = "Source Height: " & _viewModel.LeftRunner.CurrentSize.Height
        lblLSourceWidth.Text = "Source Width: " & _viewModel.LeftRunner.CurrentSize.Width

        lblRMasterHeight.Text = "Master Height: " & _viewModel.RightRunner.MasterSize.Height
        lblRMasterWidth.Text = "Master Width: " & _viewModel.RightRunner.MasterSize.Width
        lblRSourceHeight.Text = "Source Height: " & _viewModel.RightRunner.CurrentSize.Height
        lblRSourceWidth.Text = "Source Width: " & _viewModel.RightRunner.CurrentSize.Width
    End Sub

    Private Sub ProcessCrop(cropWithDefault As Rectangle, savedMasterSize As Size, currentMasterSize As Size, sourceName As String, scaling As Double,
                            boundingSize As Rectangle, positionX As Integer, positionY As Integer)
        Dim resultingCrop = _cropperMath.AdjustCrop(New CropInfo With {
                                                      .MasterSizeWithoutDefault = _cropperMath.RemoveDefaultCropSize(_cropperMath.RemoveScaling(savedMasterSize, scaling)),
                                                      .CropWithoutDefault = _cropperMath.RemoveDefaultCrop(_cropperMath.RemoveScaling(cropWithDefault, savedMasterSize, scaling))
                                                      }, _cropperMath.RemoveDefaultCropSize(_cropperMath.RemoveScaling(currentMasterSize, scaling)))


        Dim realCrop = _cropperMath.AddScaling(_cropperMath.AddDefaultCrop(resultingCrop.CropWithBlackBarsWithoutDefault), _cropperMath.AddScaling(_cropperMath.AddDefaultCropSize(resultingCrop.MasterSizeWithoutDefault), scaling), scaling)

        Obs.SetSceneItemProperties(sourceName, realCrop.Top, realCrop.Bottom, realCrop.Left, realCrop.Right, boundingSize.Width, boundingSize.Height, positionX, positionY)
        If ObsConnectionStatus2 = "Connected" Then
            _obs2.SetSceneItemProperties(sourceName, realCrop.Top, realCrop.Bottom, realCrop.Left, realCrop.Right, boundingSize.Width, boundingSize.Height, positionX, positionY)
        End If
    End Sub
    Private Sub SetNewNewMath(isRightWindow As Boolean)

        GetCurrentCropSettings(isRightWindow)
        RefreshCropperDefaultCrop()

        Dim runnerVm = If(isRightWindow, _viewModel.RightRunner, _viewModel.LeftRunner)
        Dim gameSource = If(isRightWindow, My.Settings.RightGameName, My.Settings.LeftGameName)
        Dim timerSource = If(isRightWindow, My.Settings.RightTimerName, My.Settings.LeftTimerName)
        Dim positionXTimer = If(isRightWindow, 1046, 56)
        Dim positionXGame = If(isRightWindow, 674, 48)

        Dim boundingSizeGame As New Rectangle
        boundingSizeGame.Width = 558
        boundingSizeGame.Height = 446

        Dim boundingSizeTimer As New Rectangle
        boundingSizeTimer.Width = 178
        boundingSizeTimer.Height = 47



        If runnerVm.MasterSize.Height > 0 And runnerVm.MasterSize.Width > 0 Then
            If Not String.IsNullOrWhiteSpace(gameSource) Then
                ProcessCrop(runnerVm.GameCrop.AsRectangle(),
                            runnerVm.MasterSize.AsSize(),
                            runnerVm.CurrentSize.AsSize(),
                            gameSource,
                            runnerVm.Scale,
                            boundingSizeGame,
                            positionXGame,
                            83
)
            End If

            If Not String.IsNullOrWhiteSpace(timerSource) Then
                ProcessCrop(runnerVm.TimerCrop.AsRectangle(),
                            runnerVm.MasterSize.AsSize(),
                            runnerVm.CurrentSize.AsSize(),
                            timerSource,
                            runnerVm.Scale,
                            boundingSizeTimer,
                            positionXTimer,
                            24
)
            End If

        Else
            MsgBox("Master Height/Width is 0.  Can't crop yet.", MsgBoxStyle.OkOnly, ProgramName)
        End If
    End Sub
#End Region
#Region " Refresh / Set User Info "
    Private Sub RefreshRunnerNames()
        Dim tempLeftRunner As String = cbLeftRunnerName.Text
        Dim tempRightRunner As String = cbRightRunnerName.Text
        ReuseInfo = False

        Using context As New CropDbContext
            Dim validNames = context.Crops.OrderBy(Function(r) r.Runner).Select(Function(r) New With {.RacerName = r.RunnerName}).Distinct().ToList().OrderBy(Function(r) r.RacerName, StringComparer.CurrentCultureIgnoreCase).ToList()
            cbLeftRunnerName.DataSource = validNames
            cbRightRunnerName.DataSource = validNames.ToList()
        End Using

        cbLeftRunnerName.Text = tempLeftRunner
        cbRightRunnerName.Text = tempRightRunner

        ReuseInfo = True

    End Sub
    Private Function ParsePercent(percent As String) As Double
        If (String.IsNullOrWhiteSpace(percent)) Then
            Return 1
        End If

        Return Double.Parse(percent.Replace("%", "")) / 100.0
    End Function

    Private Sub RefreshCropFromData(isRightWindow As Boolean, ByVal refreshAction As String)
        If Not Obs.IsConnected Then
            Return
        End If

        Dim savedMasterSize As Size
        Dim realMasterSize As Size
        Dim scaling As Double
        Dim savedCrop As Rectangle
        Dim realCrop As Rectangle

        Dim runnerVm = If(isRightWindow, _viewModel.RightRunner, _viewModel.LeftRunner)
        Dim runnerName = If(isRightWindow, cbRightRunnerName.Text, cbLeftRunnerName.Text)

        Using context As New CropDbContext
            Dim runnerInfo As Crop

            scaling = runnerVm.Scale
            runnerInfo = context.Crops.FirstOrDefault(Function(r) r.Submitter = My.Settings.TwitchChannel AndAlso r.RunnerName = runnerName)
            If runnerInfo Is Nothing Then
                runnerInfo = context.Crops.OrderByDescending(Function(r) r.SubmittedOn).FirstOrDefault(Function(r) r.RunnerName = runnerName)
            End If
            If runnerInfo Is Nothing Then
                runnerInfo = New Crop
                Dim initialSize = _cropperMath.RemoveDefaultCropSize(_cropperMath.RemoveScaling(GetMasterSize(isRightWindow), scaling))
                runnerInfo.SizeWidth = initialSize.Width
                runnerInfo.SizeHeight = initialSize.Height

            End If

            savedCrop = Rectangle.FromLTRB(runnerInfo.TimerCropLeft, runnerInfo.TimerCropTop, runnerInfo.TimerCropRight, runnerInfo.TimerCropBottom)
            realCrop = _cropperMath.AddDefaultCrop(savedCrop)

            savedMasterSize = New Size(runnerInfo.SizeWidth, runnerInfo.SizeHeight)

            If refreshAction = "Both" Or refreshAction = "Timer" Then
                realMasterSize = _cropperMath.AddScaling(_cropperMath.AddDefaultCropSize(savedMasterSize), scaling)
                realCrop = _cropperMath.AddScaling(realCrop, realMasterSize, runnerVm.Scale)
                runnerVm.TimerCrop.UpdateFromRectangle(realCrop)
            End If

            savedCrop = Rectangle.FromLTRB(runnerInfo.GameCropLeft, runnerInfo.GameCropTop, runnerInfo.GameCropRight, runnerInfo.GameCropBottom)
            realCrop = _cropperMath.AddDefaultCrop(savedCrop)

            If refreshAction = "Both" Or refreshAction = "Game" Then
                realCrop = _cropperMath.AddScaling(realCrop, realMasterSize, scaling)
                runnerVm.GameCrop.UpdateFromRectangle(realCrop)
                runnerVm.Twitch = runnerInfo.Runner
            End If

            If isRightWindow Then
                lblRightRunnerTwitch.Text = "Twitch: " & runnerVm.Twitch
            Else
                lblLeftRunnerTwitch.Text = "Twitch: " & runnerVm.Twitch
            End If

            runnerVm.MasterSize.UpdateFromSize(realMasterSize)
            runnerVm.Scale = scaling
        End Using


        SetHeightLabels()
    End Sub
    Private Sub RefreshObs()
        Dim lObs = Process.GetProcesses().Where(Function(pr) pr.ProcessName.StartsWith("obs", True, Globalization.CultureInfo.InvariantCulture)).ToList()

        If lObs.Count > 1 Then
            Timer1.Stop()
            GetIniFile(False, True)
            _check2NdObs = False
        ElseIf lObs.Count = 1 Then
            Dim obsProcess As New ProcessStartInfo
            Dim workDirectory As String
            workDirectory = lObs.Item(0).MainModule.FileName.Remove(lObs.Item(0).MainModule.FileName.LastIndexOf("\"), lObs.Item(0).MainModule.FileName.Length - lObs.Item(0).MainModule.FileName.LastIndexOf("\"))

            obsProcess.FileName = lObs.Item(0).MainModule.FileName
            obsProcess.WorkingDirectory = workDirectory

            Process.Start(obsProcess)
        End If
    End Sub
    Private Sub RefreshVlc()

        Dim vlcProcesses = Process.GetProcesses().Where(Function(pr) pr.ProcessName.StartsWith("vlc", True, Globalization.CultureInfo.InvariantCulture)).ToList()
        If Not vlcProcesses.Any() Then
            Exit Sub
        End If

        Dim leftVlc, rightVlc As String

        If Not String.IsNullOrWhiteSpace(cbRightVLCSource.Text) Then
            rightVlc = cbRightVLCSource.Text
        Else
            rightVlc = ""
        End If

        If Not String.IsNullOrWhiteSpace(cbLeftVLCSource.Text) Then
            leftVlc = cbLeftVLCSource.Text
        Else
            leftVlc = ""
        End If

        _vlcListLeft.Clear()
        _vlcListRight.Clear()

        Dim data = vlcProcesses.Select(Function(v) New With {.VLCName = v.MainWindowTitle}).ToList()

        _vlcListRight = _vlcListLeft.Copy

        cbLeftVLCSource.DataSource = data
        cbLeftVLCSource.DisplayMember = "VLCName"
        cbLeftVLCSource.ValueMember = "VLCName"

        cbRightVLCSource.DataSource = data.ToList()
        cbRightVLCSource.DisplayMember = "VLCName"
        cbRightVLCSource.ValueMember = "VLCName"

        cbRightVLCSource.Text = ""
        cbLeftVLCSource.Text = ""

        If Not String.IsNullOrWhiteSpace(lblLeftRunnerTwitch.Text) Then
            Dim tempText = lblLeftRunnerTwitch.Text.Remove(0, 8)
            Dim match = data.FirstOrDefault(Function(d) d.VLCName.StartsWith(tempText, True, CultureInfo.InvariantCulture))

            If match IsNot Nothing Then
                cbLeftVLCSource.Text = match.VLCName
            End If
        End If


        If Not String.IsNullOrWhiteSpace(lblRightRunnerTwitch.Text) Then
            Dim tempText = lblRightRunnerTwitch.Text.Remove(0, 8)
            Dim match = data.FirstOrDefault(Function(d) d.VLCName.StartsWith(tempText, True, CultureInfo.InvariantCulture))

            If match IsNot Nothing Then
                cbRightVLCSource.Text = match.VLCName
            End If
        End If


    End Sub
    Private Sub ClearTextBoxes(isRightWindow As Boolean, refreshAction As String)
        Dim runnerVm = If(isRightWindow, _viewModel.RightRunner, _viewModel.LeftRunner)

        If refreshAction = "Both" OrElse refreshAction = "Game" Then
            runnerVm.GameCrop.UpdateFromRectangle(Rectangle.Empty)
        End If
        If refreshAction = "Both" OrElse refreshAction = "Timer" Then
            runnerVm.TimerCrop.UpdateFromRectangle(Rectangle.Empty)
        End If
    End Sub
    Private Sub FillCurrentCropInfoFromObs(isRightWindow As Boolean)

        Dim sceneName As String = If(Obs.StudioModeEnabled, Obs.GetPreviewScene().Name, Obs.GetCurrentScene().Name)
        Dim runnerVm = If(isRightWindow, _viewModel.RightRunner, _viewModel.LeftRunner)

        runnerVm.MasterSize.UpdateFromSize(GetMasterSize(isRightWindow))

        Dim timerSource = If(isRightWindow, My.Settings.RightTimerName, My.Settings.LeftTimerName)
        Dim gameSource = If(isRightWindow, My.Settings.RightGameName, My.Settings.LeftGameName)

        If Not String.IsNullOrWhiteSpace(timerSource) Then
            runnerVm.TimerCrop.UpdateFromRectangle(Obs.GetSceneItemProperties(sceneName, timerSource).Crop)
        End If
        If Not String.IsNullOrWhiteSpace(gameSource) Then
            runnerVm.GameCrop.UpdateFromRectangle(Obs.GetSceneItemProperties(sceneName, gameSource).Crop)
        End If

    End Sub
#End Region
    Private Sub ValidateKeyPress(sender As Object, e As KeyPressEventArgs) _
        Handles txtCropRightTimer_Top.KeyPress, txtCropRightTimer_Right.KeyPress,
                txtCropRightTimer_Left.KeyPress, txtCropRightTimer_Bottom.KeyPress,
                txtCropRightGame_Top.KeyPress, txtCropRightGame_Right.KeyPress,
                txtCropRightGame_Left.KeyPress, txtCropRightGame_Bottom.KeyPress,
                txtCropLeftTimer_Top.KeyPress, txtCropLeftTimer_Right.KeyPress,
                txtCropLeftTimer_Left.KeyPress, txtCropLeftTimer_Bottom.KeyPress,
                txtCropLeftGame_Top.KeyPress, txtCropLeftGame_Right.KeyPress,
                txtCropLeftGame_Left.KeyPress, txtCropLeftGame_Bottom.KeyPress

        e.Handled = CheckIfKeyAllowed(e.KeyChar)
    End Sub

    Public Shared Function CheckIfKeyAllowed(keyChar As String) As Boolean
        Return Not ApprovedChars.Contains(keyChar) AndAlso keyChar <> vbBack
    End Function
#Region " Runner Drop Downs "
    Private Sub cbLeftRunner_KeyUp(sender As Object, e As KeyEventArgs) Handles cbLeftRunnerName.KeyUp
        Dim index As Integer
        Dim actual As String
        Dim found As String

        If ((e.KeyCode = Keys.Back) Or
    (e.KeyCode = Keys.Left) Or
    (e.KeyCode = Keys.Right) Or
    (e.KeyCode = Keys.Up) Or
    (e.KeyCode = Keys.Delete) Or
    (e.KeyCode = Keys.Down) Or
    (e.KeyCode = Keys.PageUp) Or
    (e.KeyCode = Keys.PageDown) Or
    (e.KeyCode = Keys.Home) Or
    (e.KeyCode = Keys.End)) Then

            Return
        End If

        ' Store the actual text that has been typed.
        actual = cbLeftRunnerName.Text

        ' Find the first match for the typed value.
        index = cbLeftRunnerName.FindString(actual)

        ' Get the text of the first match.
        If (index > -1) Then
            found = cbLeftRunnerName.Items(index).ToString()

            ' Select this item from the list.
            cbLeftRunnerName.SelectedIndex = index

            ' Select the portion of the text that was automatically
            ' added so that additional typing will replace it.
            cbLeftRunnerName.SelectionStart = actual.Length
            cbLeftRunnerName.SelectionLength = found.Length
        End If
    End Sub
    Private Sub cbRightRunner_KeyUp(sender As Object, e As KeyEventArgs) Handles cbRightRunnerName.KeyUp
        Dim index As Integer
        Dim actual As String
        Dim found As String

        If ((e.KeyCode = Keys.Back) Or
    (e.KeyCode = Keys.Left) Or
    (e.KeyCode = Keys.Right) Or
    (e.KeyCode = Keys.Up) Or
    (e.KeyCode = Keys.Delete) Or
    (e.KeyCode = Keys.Down) Or
    (e.KeyCode = Keys.PageUp) Or
    (e.KeyCode = Keys.PageDown) Or
    (e.KeyCode = Keys.Home) Or
    (e.KeyCode = Keys.End)) Then

            Return
        End If

        ' Store the actual text that has been typed.
        actual = cbRightRunnerName.Text

        ' Find the first match for the typed value.
        index = cbRightRunnerName.FindString(actual)

        ' Get the text of the first match.
        If (index > -1) Then
            found = cbRightRunnerName.Items(index).ToString()

            ' Select this item from the list.
            cbRightRunnerName.SelectedIndex = index

            ' Select the portion of the text that was automatically
            ' added so that additional typing will replace it.
            cbRightRunnerName.SelectionStart = actual.Length
            cbRightRunnerName.SelectionLength = found.Length
        End If
    End Sub
    Private Sub cbRightRunner_TextChanged(sender As Object, e As EventArgs) Handles cbRightRunnerName.TextChanged
        If ReuseInfo = True Then
            _viewModel.RightRunner.Name = cbRightRunnerName.Text
            ClearTextBoxes(True, "Both")
            RefreshCropFromData(True, "Both")
        End If
    End Sub
    Private Sub cbLeftRunner_TextChanged(sender As Object, e As EventArgs) Handles cbLeftRunnerName.TextChanged
        If ReuseInfo = True Then
            _viewModel.LeftRunner.Name = cbLeftRunnerName.Text
            ClearTextBoxes(False, "Both")
            RefreshCropFromData(False, "Both")
        End If
    End Sub
#End Region

#Region " Misc Functions "
    Private Sub RegisterExpertModeFeatures(ParamArray features() As Control)
        For Each control In features
            control.DataBindings.Add("Visible", My.Settings, NameOf(My.Settings.ExpertMode), False, DataSourceUpdateMode.OnPropertyChanged)
        Next
    End Sub
    Private Sub RegisterObsDependency(ParamArray features() As Control)
        For Each control In features
            control.DataBindings.Add("Enabled", _viewModel, NameOf(_viewModel.ObsConnected), False, DataSourceUpdateMode.OnPropertyChanged)
        Next
    End Sub
    Private Sub RegisterCropBindings(cropVm As CropViewModel, leftControl As Control, topControl As Control, bottomControl As Control, rightControl As Control)
        leftControl.DataBindings.Add("Text", cropVm, NameOf(cropVm.Left), False, DataSourceUpdateMode.OnPropertyChanged)
        topControl.DataBindings.Add("Text", cropVm, NameOf(cropVm.Top), False, DataSourceUpdateMode.OnPropertyChanged)
        bottomControl.DataBindings.Add("Text", cropVm, NameOf(cropVm.Bottom), False, DataSourceUpdateMode.OnPropertyChanged)
        rightControl.DataBindings.Add("Text", cropVm, NameOf(cropVm.Right), False, DataSourceUpdateMode.OnPropertyChanged)
    End Sub
    Private Sub ConfigureDataBindings()
        ' Expert mode only features
        RegisterExpertModeFeatures(lblLeftStreamlink, lblRightStreamlink)
        RegisterExpertModeFeatures(lblLeftScaling, cbLeftScaling, lblRightScaling, cbRightScaling)
        RegisterExpertModeFeatures(chkAlwaysOnTop)
        RegisterExpertModeFeatures(lblLeftVOD, lblRightVOD)
        RegisterExpertModeFeatures(lblViewLeftOnTwitch, lblViewRightOnTwitch)
        RegisterExpertModeFeatures(lblOBS2ConnectedStatus, btnConnectOBS2, btn2ndOBS)
        RegisterExpertModeFeatures(btnLeftGameDB, btnLeftTimerDB, btnRightTimerDB, btnRightGameDB)

        chkAlwaysOnTop.DataBindings.Add("Checked", My.Settings, NameOf(My.Settings.AlwaysOnTop), False, DataSourceUpdateMode.OnPropertyChanged)
        DataBindings.Add("TopMost", My.Settings, NameOf(My.Settings.AlwaysOnTop), False, DataSourceUpdateMode.OnPropertyChanged)

        'All OBS dependencies
        RegisterObsDependency(btnSetLeftCrop, btnSetRightCrop)
        RegisterObsDependency(btnGetLeftCrop, btnGetRightCrop)
        RegisterObsDependency(btnSaveLeftCrop, btnSaveRightCrop)
        RegisterObsDependency(btnSetLeftVLC, btnSetRightVLC, btnGetProcesses, cbLeftVLCSource, cbRightVLCSource)
        RegisterObsDependency(btnSetTrackCommNames, btnNewLeftRunner, btnNewRightRunner)
        RegisterObsDependency(btnSyncWithServer, btn2ndOBS, btnConnectOBS2)
        RegisterObsDependency(gbTrackerComms, gbLeftGameWindow, gbRightGameWindow, gbLeftTimerWindow, gbRightTimerWindow)
        RegisterObsDependency(cbLeftRunnerName, cbRightRunnerName)
        RegisterObsDependency(lblLeftVOD, lblRightVOD)
        RegisterObsDependency(lblViewLeftOnTwitch, lblViewRightOnTwitch)
        RegisterObsDependency(lblLeftStreamlink, lblRightStreamlink)

        'Bind all crop info
        RegisterCropBindings(_viewModel.LeftRunner.GameCrop,
                             txtCropLeftGame_Left,
                             txtCropLeftGame_Top,
                             txtCropLeftGame_Bottom,
                             txtCropLeftGame_Right)
        RegisterCropBindings(_viewModel.LeftRunner.TimerCrop,
                             txtCropLeftTimer_Left,
                             txtCropLeftTimer_Top,
                             txtCropLeftTimer_Bottom,
                             txtCropLeftTimer_Right)
        RegisterCropBindings(_viewModel.RightRunner.GameCrop,
                             txtCropRightGame_Left,
                             txtCropRightGame_Top,
                             txtCropRightGame_Bottom,
                             txtCropRightGame_Right)
        RegisterCropBindings(_viewModel.RightRunner.TimerCrop,
                             txtCropRightTimer_Left,
                             txtCropRightTimer_Top,
                             txtCropRightTimer_Bottom,
                             txtCropRightTimer_Right)

    End Sub
    Private Sub OBSWebScocketCropper_Load(sender As Object, e As EventArgs) Handles Me.Load

        RefreshCropperDefaultCrop()

        ReuseInfo = True
        lblOBS1ConnectedStatus.Text = "Not Connected"
        lblOBS2ConnectedStatus.Text = "Not Connected"
        ConfigureDataBindings()
        CreateNewSourceTable()

        If My.Settings.HasFinishedWelcome = False Then
            Dim uSettings As New UserSettings

            UserSettings.ShowVLCOption = True
            uSettings.ShowDialog(Me)

            If My.Settings.HasFinishedWelcome = False Then
                MsgBox("There are no default settings loaded.  Program will close.  Please change and then save some settings before continuing.", MsgBoxStyle.OkOnly, ProgramName)
                Close()
            End If

            CheckUnusedFields()

            If ObsSettingsResult = "VLC" Then
                VlcSettings.ShowDialog(Me)

            End If

            RefreshRunnerNames()
        Else
            ResetHeightWidthLabels()

            RefreshRunnerNames()

            RefreshCropperDefaultCrop()

            CheckUnusedFields()
        End If

        ExpertModeToolStripMenuItem.Checked = My.Settings.ExpertMode
    End Sub
    Private Sub AboutToolStripMenuItem1_Click(sender As Object, e As EventArgs) Handles AboutToolStripMenuItem.Click
        About.ShowDialog(Me)
    End Sub
    Private Sub ExitToolStripMenuItem_Click_1(sender As Object, e As EventArgs) Handles ExitToolStripMenuItem.Click
        Close()
    End Sub
    Private Sub ChangeVLCSettingsToolStripMenuItem_Click_1(sender As Object, e As EventArgs) Handles ChangeVLCSettingsToolStripMenuItem.Click
        VlcSettings.ShowDialog(Me)

        RefreshCropperDefaultCrop()
    End Sub
    Private Sub ExpertModeToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ExpertModeToolStripMenuItem.Click
        My.Settings.ExpertMode = ExpertModeToolStripMenuItem.Checked
        My.Settings.Save()

    End Sub
    Private Sub ChangeUserSettingsToolStripMenuItem_Click_1(sender As Object, e As EventArgs) Handles ChangeUserSettingsToolStripMenuItem.Click
        Dim uSettings As New UserSettings

        UserSettings.ShowVLCOption = False
        uSettings.ShowDialog(Me)

        If My.Settings.HasFinishedWelcome = False Then
            MsgBox("There are no default settings loaded.  Program will close.  Please change and then save some settings before continuing.", MsgBoxStyle.OkOnly, ProgramName)
            Close()

        Else
            RefreshRunnerNames()
            RefreshCropperDefaultCrop()
            CheckUnusedFields()
        End If
    End Sub
    Private Sub GetSyncFromServer()

        Dim cropList As IEnumerable(Of RunnerInfo)
        Try
            cropList = _cropApi.GetCrops()

        Catch ex As Exception
            MessageBox.Show(Me, "Error While retrieving data from server: " & ex.ToString())
            Return
        End Try


        Using context As New CropDbContext

            Dim validGuids As New List(Of Guid)

            For Each runnerInfo In cropList
                Dim runnerInfoCopy = runnerInfo
                Dim existingCrops = context.Crops.Where(Function(x) x.Runner = runnerInfoCopy.Runner)

                For Each crop In runnerInfo.Crops
                    If Not crop.Id.HasValue Then
                        Throw New ArgumentNullException(NameOf(crop.Id))
                    End If

                    Dim id = If(crop.Id, Guid.Empty)

                    validGuids.Add(id)
                    Dim matchingItem = If(existingCrops.FirstOrDefault(Function(x) x.Id = id),
                        New Crop With {.Runner = runnerInfo.Runner})

                    matchingItem.SizeWidth = crop.Size.Width
                    matchingItem.SizeHeight = crop.Size.Height
                    matchingItem.GameCropTop = crop.GameCrop.Top
                    matchingItem.GameCropBottom = crop.GameCrop.Bottom
                    matchingItem.GameCropRight = crop.GameCrop.Right
                    matchingItem.GameCropLeft = crop.GameCrop.Left
                    matchingItem.TimerCropTop = crop.TimerCrop.Top
                    matchingItem.TimerCropBottom = crop.TimerCrop.Bottom
                    matchingItem.TimerCropRight = crop.TimerCrop.Right
                    matchingItem.TimerCropLeft = crop.TimerCrop.Left
                    matchingItem.Submitter = crop.Submitter

                    matchingItem.SubmittedOn = crop.SubmittedOn

                    If crop.RunnerName Is Nothing Then
                        matchingItem.RunnerName = runnerInfo.Runner
                    Else
                        matchingItem.RunnerName = crop.RunnerName
                    End If

                    If matchingItem.Id <> crop.Id Then
                        matchingItem.Id = id
                        context.Crops.Add(matchingItem)
                    End If

                Next
            Next

            'This will need to be updated back to raw sql in context.Database.ExecuteSqlCommand
            Dim nonExistingItems = context.Crops.Where(Function(x) Not validGuids.Contains(x.Id))
            For Each item In nonExistingItems
                item.SubmittedOn = Nothing
            Next

            context.SaveChanges()
        End Using

        RefreshRunnerNames()
    End Sub
    Private Sub GetIniFile(python As Boolean, resetWebSocketPort As Boolean)

        Dim appDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)

        Dim fileName As String = appDataPath & "\obs-studio\global.ini"

        Dim iniContents As Dictionary(Of String, Dictionary(Of String, String)) = IniParser.ParseFile(fileName)

        For Each sectionName As String In iniContents.Keys
            For Each valueName As String In iniContents(sectionName).Keys
                Dim value As String = iniContents(sectionName)(valueName)

                '[SectionName]
                'ValueName=Value
                'ValueName=Value
                '
                'SectionName: The name of the current section (ex: Jones).
                'ValueName  : The name of the current value   (ex: Email).
                'Value      : The value of [ValueName]        (ex: josh.jones@gmail.com).

                If python = True Then
                    If sectionName.ToLower = "python" Then
                        If valueName.ToLower = "path64bit" Then
                            MsgBox(value, MsgBoxStyle.OkOnly)
                            ''IniParser.WritePrivateProfileStringW(SectionName, ValueName, Value & "_Test", FileName)
                        End If
                    End If
                Else
                    If sectionName.ToLower = "websocketapi" Then
                        If valueName.ToLower = "serverport" Then
                            If resetWebSocketPort = True Then
                                IniParser.WritePrivateProfileStringW(sectionName, valueName, My.Settings.ConnectionPort1.ToString, fileName)
                            Else
                                IniParser.WritePrivateProfileStringW(sectionName, valueName, My.Settings.ConnectionPort2.ToString, fileName)
                            End If


                        End If
                    End If
                End If

            Next
        Next
    End Sub
    Private Sub SendToServer()
        Using context As New CropDbContext
            Dim unsentData = context.Crops.Where(Function(crop) Not crop.SubmittedOn.HasValue)
            Try
                For Each localRunner In unsentData

                    Dim runner As New RunnerCropAdd With {
                            .Size = localRunner.Size,
                            .GameCrop = localRunner.GameCrop,
                            .TimerCrop = localRunner.TimerCrop,
                            .Runner = localRunner.Runner,
                            .RunnerName = localRunner.RunnerName,
                            .Submitter = localRunner.Submitter,
                            .Id = localRunner.Id
                            }

                    _cropApi.UpdateCrop(runner)
                    localRunner.SubmittedOn = runner.SubmittedOn

                Next
            Finally
                'save any changes already made, hopefully all of them.
                context.SaveChanges()
            End Try

        End Using

    End Sub
    Private Sub RefreshCropperDefaultCrop()
        _cropperMath.DefaultCrop = Rectangle.FromLTRB(0, My.Settings.DefaultCropTop, 0, My.Settings.DefaultCropBottom)
    End Sub
    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        _lastUpdate = _lastUpdate + 1

        If _lastUpdate > 5 Then
            _lastUpdate = 0
            If _check2NdObs = True Then
                RefreshObs()
            End If
        End If
    End Sub
    Private Sub ConnectToObs2()
        Cursor = Cursors.WaitCursor

        Try

            If Not _obs2.IsConnected Then

                Dim isPortOpen As Boolean = _obs2.IsPortOpen(ConnectionString2)

                If Not isPortOpen Then
                    MsgBox("OBS2 WebSocket is not running.  Please make sure the OBS2 WebSocket is enabled before continuing!", MsgBoxStyle.OkOnly, ProgramName)
                Else
                    _obs2.Connect(ConnectionString2, My.Settings.Password2)
                End If
            ElseIf MsgBox("This connection is already connected.  Do you wish to disconnect?", MsgBoxStyle.YesNo, ProgramName) = MsgBoxResult.Yes Then
                _obs2.Disconnect()
            End If

        Finally
            Cursor = Cursors.Default
        End Try

    End Sub
    Public Sub ConnectToObs()
        Cursor = Cursors.WaitCursor
        Try

            If Obs.IsConnected = False Then
                Dim isPortOpen As Boolean = Obs.IsPortOpen(ConnectionString)
                If isPortOpen = False Then
                    MsgBox("OBS WebSocket is not running.  Please make sure the OBS WebSocket is enabled before continuing!", MsgBoxStyle.OkOnly, ProgramName)
                Else
                    Obs.Connect(ConnectionString, My.Settings.Password1)
                End If
            ElseIf MsgBox("This connection is already connected.  Do you wish to disconnect?", MsgBoxStyle.YesNo, ProgramName) = MsgBoxResult.Yes Then
                Obs.Disconnect()
            End If

        Finally
            Cursor = Cursors.Default
        End Try

    End Sub
    Private Sub CheckUnusedFields()
        Dim visComms, visLeftRunner, visRightRunner,
        visLeftTracker, visRightTracker, visLeftTimer, visLeftGame,
        visRightTimer, visRightGame As Boolean

        visComms = Not String.IsNullOrWhiteSpace(My.Settings.CommentaryOBS)
        txtCommentaryNames.Visible = visComms
        lblCommentary.Visible = visComms


        visLeftRunner = Not String.IsNullOrWhiteSpace(My.Settings.LeftRunnerOBS) OrElse
            Not String.IsNullOrWhiteSpace(My.Settings.LeftGameName) OrElse
            Not String.IsNullOrWhiteSpace(My.Settings.LeftTimerName)
        cbLeftRunnerName.Visible = visLeftRunner
        lblLeftRunner.Visible = visLeftRunner

        visRightRunner = Not String.IsNullOrWhiteSpace(My.Settings.RightRunnerOBS) OrElse
            Not String.IsNullOrWhiteSpace(My.Settings.RightGameName) OrElse
            Not String.IsNullOrWhiteSpace(My.Settings.RightTimerName)
        cbRightRunnerName.Visible = visRightRunner
        lblRightRunner.Visible = visRightRunner

        visLeftTracker = Not String.IsNullOrWhiteSpace(My.Settings.LeftTrackerOBS)
        txtLeftTrackerURL.Visible = visLeftTracker
        lblLeftTracker.Visible = visLeftTracker

        visRightTracker = Not String.IsNullOrWhiteSpace(My.Settings.RightTrackerOBS)
        txtRightTrackerURL.Visible = visRightTracker
        lblRightTracker.Visible = visRightTracker

        visLeftTimer = Not String.IsNullOrWhiteSpace(My.Settings.LeftTimerName)
        gbLeftTimerWindow.Visible = visLeftTimer

        visLeftGame = Not String.IsNullOrWhiteSpace(My.Settings.LeftGameName)
        gbLeftGameWindow.Visible = visLeftGame

        visRightTimer = Not String.IsNullOrWhiteSpace(My.Settings.RightTimerName)
        gbRightTimerWindow.Visible = visRightTimer

        visRightGame = Not String.IsNullOrWhiteSpace(My.Settings.RightGameName)
        gbRightGameWindow.Visible = visRightGame

        If visRightGame = False And visRightTimer = False Then
            btnSaveRightCrop.Visible = False
            btnGetRightCrop.Visible = False
            btnSetRightCrop.Visible = False
            btnSetRightVLC.Visible = False
            lblRightVLC.Visible = False
            cbRightVLCSource.Visible = False
        Else
            btnSaveRightCrop.Visible = True
            btnGetRightCrop.Visible = True
            btnSetRightCrop.Visible = True
            btnSetRightVLC.Visible = True
            lblRightVLC.Visible = True
            cbRightVLCSource.Visible = True
        End If

        If visLeftGame = False And visLeftTimer = False Then
            btnSaveLeftCrop.Visible = False
            btnGetLeftCrop.Visible = False
            btnSetLeftCrop.Visible = False
            btnSetLeftVLC.Visible = False
            lblLeftVLC.Visible = False
            cbLeftVLCSource.Visible = False
        Else
            btnSaveLeftCrop.Visible = True
            btnGetLeftCrop.Visible = True
            btnSetLeftCrop.Visible = True
            btnSetLeftVLC.Visible = True
            lblLeftVLC.Visible = True
            cbLeftVLCSource.Visible = True
        End If
    End Sub
    Private Sub OBSWebSocketCropper_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If (e.KeyCode = Keys.S AndAlso e.Modifiers = Keys.Control) Then
            SyncWithServer()
        ElseIf (e.KeyCode = Keys.Q AndAlso e.Modifiers = Keys.Control) Then
            Try
                SetNewNewMath(False)
            Catch ex As ErrorResponseException
                MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
            End Try
        ElseIf (e.KeyCode = Keys.W AndAlso e.Modifiers = Keys.Control) Then
            Try
                FillCurrentCropInfoFromObs(False)
            Catch ex As ErrorResponseException
                MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
            End Try
        ElseIf (e.KeyCode = Keys.E AndAlso e.Modifiers = Keys.Control) Then
            Try
                SaveRunnerCrop(False)
            Catch ex As ErrorResponseException
                MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
            End Try
        ElseIf (e.KeyCode = Keys.R AndAlso e.Modifiers = Keys.Control) Then
            Try
                SetNewNewMath(True)
            Catch ex As ErrorResponseException
                MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
            End Try
        ElseIf (e.KeyCode = Keys.T AndAlso e.Modifiers = Keys.Control) Then
            Try
                FillCurrentCropInfoFromObs(True)
            Catch ex As ErrorResponseException
                MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
            End Try
        ElseIf (e.KeyCode = Keys.Y AndAlso e.Modifiers = Keys.Control) Then
            Try
                SaveRunnerCrop(True)
            Catch ex As ErrorResponseException
                MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
            End Try
        End If
    End Sub

    Private Sub AddNewRunner(isRightWindow As Boolean)
        NewRunnerName = ""
        NewRunnerTwitch = ""
        GetObsInfo = False
        ReuseInfo = True

        Dim dResult = NewRunner.ShowDialog(Me)
        Dim runnerNameField = If(isRightWindow, cbRightRunnerName, cbLeftRunnerName)
        Dim runnerTwitchField = If(isRightWindow, lblRightRunnerTwitch, lblLeftRunnerTwitch)

        If dResult = DialogResult.OK Then
            Dim runnerVm = If(isRightWindow, _viewModel.RightRunner, _viewModel.LeftRunner)

            If Not String.IsNullOrWhiteSpace(NewRunnerName) Then
                runnerVm.Name = NewRunnerName
                runnerNameField.Text = NewRunnerName
            End If

            runnerVm.Twitch = If(String.IsNullOrWhiteSpace(NewRunnerTwitch), NewRunnerName, NewRunnerTwitch)
            runnerTwitchField.Text = "Twitch: " & runnerVm.Twitch

            If GetObsInfo = True Then
                If runnerVm.MasterSize.Height = 0 OrElse runnerVm.MasterSize.Width = 0 Then
                    runnerVm.MasterSize.UpdateFromSize(GetMasterSize(isRightWindow))
                End If

                Try
                    SetNewNewMath(isRightWindow)
                Catch ex As ErrorResponseException
                    MessageBox.Show(Me, "Error while getting information from OBS. Are you sure you are on the correct scene?")
                End Try
            End If
        End If

        GetObsInfo = False
        ReuseInfo = True
    End Sub
    Private Sub lblViewLeftOnTwitch_Click(sender As Object, e As EventArgs) Handles lblViewLeftOnTwitch.Click
        If Not String.IsNullOrWhiteSpace(_viewModel.LeftRunner.Twitch) Then
            Process.Start("https://twitch.tv/" & _viewModel.LeftRunner.Twitch)
        End If
    End Sub
    Private Sub lblViewRightOnTwitch_Click(sender As Object, e As EventArgs) Handles lblViewRightOnTwitch.Click
        If Not String.IsNullOrWhiteSpace(_viewModel.RightRunner.Twitch) Then
            Process.Start("https://twitch.tv/" & _viewModel.RightRunner.Twitch)
        End If
    End Sub
    Private Sub lblLeftVOD_Click(sender As Object, e As EventArgs) Handles lblLeftVOD.Click
        If Not String.IsNullOrWhiteSpace(_viewModel.LeftRunner.Twitch) Then
            Process.Start("https://twitch.tv/" & _viewModel.LeftRunner.Twitch & "/videos/all")
        End If
    End Sub
    Private Sub lblRightVOD_Click(sender As Object, e As EventArgs) Handles lblRightVOD.Click
        If Not String.IsNullOrWhiteSpace(_viewModel.RightRunner.Twitch) Then
            Process.Start("https://twitch.tv/" & _viewModel.RightRunner.Twitch & "/videos/all")
        End If
    End Sub

    Private Sub StartStreamlink(twitch As String, isRightWindow As Boolean)
        Dim replacedPath = My.Settings.StreamlinkPath?.Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
        If replacedPath Is Nothing OrElse Not File.Exists(replacedPath) Then
            Dim initialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Streamlink", "bin")
            Dim fsDialog As New OpenFileDialog
            fsDialog.FileName = "streamlink.exe"
            fsDialog.Title = "Please provide the path to streamlink.exe"
            fsDialog.Filter = "Exe files |*.exe"
            fsDialog.InitialDirectory = initialPath
            fsDialog.CheckFileExists = True

            Dim result = fsDialog.ShowDialog(Me)

            If result <> DialogResult.OK OrElse Not File.Exists(fsDialog.FileName) Then
                Exit Sub
            End If

            My.Settings.StreamlinkPath = fsDialog.FileName
            My.Settings.Save()

            replacedPath = fsDialog.FileName
        End If

        Dim streamLinkArguments As String = $"--player-args=""--file-caching 2000 --no-one-instance --network-caching 2000 --input-title-format {twitch} {{filename}}"" https://www.twitch.tv/{twitch} best --player-continuous-http --player-no-close"

        Dim customArgs = If(isRightWindow, My.Settings.RightStreamlinkVlcParams, My.Settings.LeftStreamlinkVlcParams)
        If Not String.IsNullOrWhiteSpace(customArgs) Then
            streamLinkArguments = customArgs
        End If


        Dim myProcess = New Process
        myProcess.StartInfo = New ProcessStartInfo With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WindowStyle = ProcessWindowStyle.Hidden,
            .FileName = replacedPath,
            .Arguments = streamLinkArguments,
        .RedirectStandardError = False,
            .RedirectStandardOutput = True
                        }

        myProcess.Start()
    End Sub
    Private Sub lblLeftStreamlink_Click(sender As Object, e As EventArgs) Handles lblLeftStreamlink.Click
        If Not String.IsNullOrWhiteSpace(_viewModel.LeftRunner.Twitch) Then
            StartStreamlink(_viewModel.LeftRunner.Twitch, False)
        Else
            MsgBox("No Runner selected, cannot continue.")
        End If
    End Sub
    Private Sub lblRightStreamlink_Click(sender As Object, e As EventArgs) Handles lblRightStreamlink.Click
        If Not String.IsNullOrWhiteSpace(_viewModel.RightRunner.Twitch) Then
            StartStreamlink(_viewModel.RightRunner.Twitch, True)
        Else
            MsgBox("No Runner selected, cannot continue.")
        End If
    End Sub
    Private Sub btnTestSourceSettings_Click(sender As Object, e As EventArgs) Handles btnTestSourceSettings.Click
        Dim rightGameSourceInfo As SourceSettings
        Dim commentarySizeInfo As SceneItemProperties
        Dim commentaryFontInfo As TextGDI
        Dim micIconInfo As SceneItemProperties


        'micIconInfo = Obs.GetSceneItemProperties("", "MicIcon")
        commentarySizeInfo = Obs.GetSceneItemProperties("", My.Settings.LeftGameName)
        rightGameSourceInfo = Obs.GetSourceSettings(My.Settings.CommentaryOBS)
        commentaryFontInfo = Obs.GetTextGDIProperties(My.Settings.CommentaryOBS)


        If commentaryFontInfo IsNot Nothing Then
            Dim textString As String
            Dim fontSize As Integer
            Dim fontName As String

            fontName = commentaryFontInfo.FontFace
            fontSize = commentaryFontInfo.FontSize

            Dim textFont As New Font(fontName, fontSize)

            textString = commentaryFontInfo.text

            Dim comSize As Size = TextRenderer.MeasureText(textString, textFont)

            Dim sizeOfString As SizeF
            Dim g As Graphics = CreateGraphics()
            sizeOfString = g.MeasureString(textString, textFont)

            Dim micX As Integer = CInt(commentarySizeInfo.PositionX - (sizeOfString.Width / 3))

            Obs.SetSceneItemPosition("MicIcon", micX, CInt(micIconInfo.PositionY))

        End If
    End Sub


    Private Function TransScale(rect As Rectangle, originalSize As Size, originalScaling As Double, newSize As Size, newScaling As Double) As Rectangle
        Return _cropperMath.AddScaling(_cropperMath.RemoveScaling(rect, originalSize, originalScaling), newSize, newScaling)
    End Function

    Private Sub AdjustScaling(runnerVm As RunnerViewModel, newScale As Double)
        If Math.Abs(newScale - runnerVm.Scale) < 0.0001 OrElse Math.Abs(runnerVm.Scale) < 0.0001 Then
            Exit Sub
        End If

        Dim newSize = _cropperMath.AddScaling(_cropperMath.RemoveScaling(runnerVm.MasterSize.AsSize(), runnerVm.Scale), newScale)
        runnerVm.GameCrop.UpdateFromRectangle(TransScale(runnerVm.GameCrop.AsRectangle(),
                                                         runnerVm.MasterSize.AsSize(),
                                                         runnerVm.Scale,
                                                         newSize,
                                                         newScale))
        runnerVm.TimerCrop.UpdateFromRectangle(TransScale(runnerVm.TimerCrop.AsRectangle(),
                                                          runnerVm.MasterSize.AsSize(),
                                                          runnerVm.Scale,
                                                          newSize,
                                                          newScale))
        runnerVm.MasterSize.UpdateFromSize(newSize)
        runnerVm.Scale = newScale
    End Sub

    Private Sub cbLeftScaling_TextChanged(sender As Object, e As EventArgs) Handles cbLeftScaling.TextChanged
        AdjustScaling(_viewModel.LeftRunner, ParsePercent(cbLeftScaling.Text))
    End Sub
    Private Sub cbRightScaling_TextChanged(sender As Object, e As EventArgs) Handles cbRightScaling.TextChanged
        AdjustScaling(_viewModel.RightRunner, ParsePercent(cbRightScaling.Text))
    End Sub
    Private Sub Uncrop(ByVal sourceName As String)
        DispatchToObs(Sub(o) o.SetSceneItemProperties(sourceName, 0 + My.Settings.DefaultCropTop, 0 + My.Settings.DefaultCropBottom, 0, 0, 0, 0, 0, 0))
    End Sub

    Private Sub ObsWebSocketCropper_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        My.Settings.Save()
    End Sub

#End Region
End Class
