Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class Form1
    Inherits Form

    Private Const DEFAULT_PORT As Integer = 9988
    Private Const CELL_COUNT As Integer = 12
    Private Const ANIM_DELAY_MS As Integer = 400

    Private game As OAQGame
    Private peer As NetworkPeer
    Private isHost As Boolean
    Private localPlayer As Integer = -1
    Private selectedCell As Integer = -1

    ' === UI connect ===
    Private pnlConnect As Panel
    Private lblPort As Label
    Private txtPort As TextBox
    Private lblIP As Label
    Private txtIP As TextBox
    Private btnHost As Button
    Private btnJoin As Button
    Private lblStatus As Label

    ' === UI game ===
    Private pnlGame As Panel
    Private boardPanel As Panel       ' GDI board
    Private lblTurn As Label
    Private lblYouAre As Label
    Private lblScore1 As Label
    Private lblScore2 As Label
    Private btnDirLeft As Button
    Private btnDirRight As Button
    Private btnRestart As Button
    Private lstLog As ListBox

    ' === Animation ===
    Private animTimer As System.Windows.Forms.Timer
    Private animSteps As List(Of Integer)
    Private animStep As Integer
    Private animPendingDir As OAQGame.GameDirection
    Private animPendingCell As Integer
    Private animPendingPlayer As Integer
    Private animIsRunning As Boolean = False
    Private animHighlightCell As Integer = -1   ' o dang duoc highlight trong animation

    ' === Board geometry (tinh khi resize / build) ===
    ' Layout:
    '   [Q0]  [7][8][9][10][11]  [Q1]   <- hang tren (Player2), y=topRow
    '   [Q0]  [1][2][3][ 4][ 5]  [Q1]   <- hang duoi (Player1), y=botRow
    ' Q0 o x=0, Q1 o x=rightCol
    Private Const BW As Integer = 920   ' board panel width
    Private Const BH As Integer = 300   ' board panel height
    Private Const QUAN_W As Integer = 80
    Private Const DAN_W As Integer = 112
    Private Const DAN_H As Integer = 120
    Private Const QUAN_H As Integer = DAN_H * 2 + 4
    Private Const ROW_TOP As Integer = 10
    Private Const ROW_BOT As Integer = ROW_TOP + DAN_H + 4
    Private Const DAN_START_X As Integer = QUAN_W + 8

    ' cellRect(i) -> Rectangle vi tri o tren boardPanel
    Private cellRect(CELL_COUNT - 1) As Rectangle

    Public Sub New()
        BuildCellRects()
        InitUI()
    End Sub

    Private Sub BuildCellRects()
        ' Q0 (index 0): trai, cao ca 2 hang
        cellRect(0) = New Rectangle(4, ROW_TOP, QUAN_W, QUAN_H)
        ' Q1 (index 6): phai
        cellRect(6) = New Rectangle(BW - QUAN_W - 4, ROW_TOP, QUAN_W, QUAN_H)
        ' Hang Player2 (index 7..11): hang tren, tu trai sang phai
        Dim i As Integer
        For i = 7 To 11
            Dim col As Integer = i - 7
            cellRect(i) = New Rectangle(DAN_START_X + col * (DAN_W + 2), ROW_TOP, DAN_W, DAN_H)
        Next i
        ' Hang Player1 (index 1..5): hang duoi, tu trai sang phai
        For i = 1 To 5
            Dim col As Integer = i - 1
            cellRect(i) = New Rectangle(DAN_START_X + col * (DAN_W + 2), ROW_BOT, DAN_W, DAN_H)
        Next i
    End Sub

    Private Sub InitUI()
        Me.Text = "O An Quan Online - 2CongLC"
        Me.ClientSize = New Size(BW, 620)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(245, 240, 220)

        animTimer = New System.Windows.Forms.Timer()
        animTimer.Interval = ANIM_DELAY_MS
        AddHandler animTimer.Tick, AddressOf AnimTimer_Tick

        BuildConnectPanel()
        BuildGamePanel()
        pnlGame.Visible = False
    End Sub

    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Location = New Point(0, 0)
        pnlConnect.Size = New Size(BW, 620)
        pnlConnect.BackColor = Color.FromArgb(245, 240, 220)

        Dim lblTitle As New Label()
        lblTitle.Text = "O AN QUAN - CHOI ONLINE PVP"
        lblTitle.Font = New Font("Segoe UI", 16.0!, FontStyle.Bold)
        lblTitle.Location = New Point(240, 60)
        lblTitle.AutoSize = True
        pnlConnect.Controls.Add(lblTitle)

        lblPort = New Label() : lblPort.Text = "Port:" : lblPort.Location = New Point(330, 150) : lblPort.AutoSize = True
        pnlConnect.Controls.Add(lblPort)
        txtPort = New TextBox() : txtPort.Text = DEFAULT_PORT.ToString() : txtPort.Location = New Point(400, 147) : txtPort.Width = 80
        pnlConnect.Controls.Add(txtPort)

        btnHost = New Button() : btnHost.Text = "Tao phong (Host)" : btnHost.Location = New Point(330, 190) : btnHost.Size = New Size(180, 36)
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        lblIP = New Label() : lblIP.Text = "IP cua Host:" : lblIP.Location = New Point(330, 250) : lblIP.AutoSize = True
        pnlConnect.Controls.Add(lblIP)
        txtIP = New TextBox() : txtIP.Text = "127.0.0.1" : txtIP.Location = New Point(440, 247) : txtIP.Width = 140
        pnlConnect.Controls.Add(txtIP)

        btnJoin = New Button() : btnJoin.Text = "Vao phong (Join)" : btnJoin.Location = New Point(330, 285) : btnJoin.Size = New Size(180, 36)
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lblStatus = New Label()
        lblStatus.Text = "Host: bam 'Tao phong' roi gui IP/port cho ban choi cung." & Environment.NewLine &
                         "Khach: nhap IP cua Host roi bam 'Vao phong'." & Environment.NewLine &
                         "Choi qua Internet can mo port (port forward) tren router cua Host."
        lblStatus.Location = New Point(240, 350) : lblStatus.AutoSize = True : lblStatus.ForeColor = Color.DimGray
        pnlConnect.Controls.Add(lblStatus)
        Me.Controls.Add(pnlConnect)
    End Sub

    Private Sub BuildGamePanel()
        pnlGame = New Panel()
        pnlGame.Location = New Point(0, 0)
        pnlGame.Size = New Size(BW, 620)
        pnlGame.BackColor = Color.FromArgb(245, 240, 220)

        ' GDI board panel
        boardPanel = New Panel()
        boardPanel.Location = New Point(0, 60)
        boardPanel.Size = New Size(BW, BH)
        boardPanel.BackColor = Color.FromArgb(210, 180, 120)
        boardPanel.Cursor = Cursors.Hand
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        AddHandler boardPanel.MouseDown, AddressOf BoardPanel_MouseDown
        pnlGame.Controls.Add(boardPanel)

        lblTurn = New Label() : lblTurn.Location = New Point(20, 15) : lblTurn.AutoSize = True
        lblTurn.Font = New Font("Segoe UI", 12.0!, FontStyle.Bold)
        pnlGame.Controls.Add(lblTurn)

        lblYouAre = New Label() : lblYouAre.Location = New Point(20, 38) : lblYouAre.AutoSize = True
        pnlGame.Controls.Add(lblYouAre)

        lblScore1 = New Label() : lblScore1.Location = New Point(600, 15) : lblScore1.AutoSize = True
        pnlGame.Controls.Add(lblScore1)
        lblScore2 = New Label() : lblScore2.Location = New Point(600, 38) : lblScore2.AutoSize = True
        pnlGame.Controls.Add(lblScore2)

        Dim btnY As Integer = 60 + BH + 10

        btnDirLeft = New Button() : btnDirLeft.Text = "Di TRAI" : btnDirLeft.Location = New Point(300, btnY)
        btnDirLeft.Size = New Size(130, 36) : btnDirLeft.Enabled = False
        AddHandler btnDirLeft.Click, AddressOf BtnDirLeft_Click
        pnlGame.Controls.Add(btnDirLeft)

        btnDirRight = New Button() : btnDirRight.Text = "Di PHAI" : btnDirRight.Location = New Point(445, btnY)
        btnDirRight.Size = New Size(130, 36) : btnDirRight.Enabled = False
        AddHandler btnDirRight.Click, AddressOf BtnDirRight_Click
        pnlGame.Controls.Add(btnDirRight)

        btnRestart = New Button() : btnRestart.Text = "Choi lai (chi Host)" : btnRestart.Location = New Point(650, btnY)
        btnRestart.Size = New Size(160, 36)
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        pnlGame.Controls.Add(btnRestart)

        lstLog = New ListBox()
        lstLog.Location = New Point(10, btnY + 50)
        lstLog.Size = New Size(BW - 20, 150)
        pnlGame.Controls.Add(lstLog)

        Me.Controls.Add(pnlGame)
    End Sub

    ' ==========================================================
    '  GDI PAINT
    ' ==========================================================
    Private Sub BoardPanel_Paint(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
            DrawCell(g, i)
        Next i
    End Sub

    Private Sub DrawCell(g As Graphics, idx As Integer)
        Dim r As Rectangle = cellRect(idx)
        Dim isQuan As Boolean = (game IsNot Nothing AndAlso game.IsQuan(idx))
        Dim isSelected As Boolean = (idx = selectedCell)
        Dim isAnimHL As Boolean = (idx = animHighlightCell)

        ' --- Nen o ---
        Dim fillColor As Color
        If isAnimHL Then
            fillColor = Color.FromArgb(255, 230, 80)        ' vang tuoi khi animation
        ElseIf isSelected Then
            fillColor = Color.FromArgb(140, 210, 140)       ' xanh la khi chon
        ElseIf isQuan Then
            fillColor = Color.FromArgb(200, 155, 60)        ' nau vang - o Quan
        ElseIf game IsNot Nothing AndAlso game.OwnerOf(idx) = 0 Then
            fillColor = Color.FromArgb(180, 210, 240)       ' xanh nhat - P1
        ElseIf game IsNot Nothing AndAlso game.OwnerOf(idx) = 1 Then
            fillColor = Color.FromArgb(180, 230, 185)       ' xanh la nhat - P2
        Else
            fillColor = Color.FromArgb(200, 180, 130)
        End If

        Using br As New SolidBrush(fillColor)
            g.FillRectangle(br, r)
        End Using

        ' Vien o
        Dim borderColor As Color = If(isSelected OrElse isAnimHL, Color.DarkGreen, Color.FromArgb(120, 90, 40))
        Dim borderW As Integer = If(isSelected OrElse isAnimHL, 3, 1)
        Using pen As New Pen(borderColor, borderW)
            g.DrawRectangle(pen, r)
        End Using

        ' --- Ten o Quan ---
        If isQuan Then
            Using fnt As New Font("Segoe UI", 8.0!, FontStyle.Bold)
                Using br As New SolidBrush(Color.DarkRed)
                    Dim label As String = "QUAN"
                    Dim sz As SizeF = g.MeasureString(label, fnt)
                    g.DrawString(label, fnt, br, r.Left + (r.Width - sz.Width) / 2.0F, r.Top + 4)
                End Using
            End Using
        End If

        ' --- Ve cac vien da ---
        Dim stones As Integer = If(game IsNot Nothing, game.Stones(idx), 0)
        If stones > 0 Then
            DrawStones(g, r, stones, isQuan)
        End If
    End Sub

    ' Ve cac vien da trong mot o
    Private Sub DrawStones(g As Graphics, r As Rectangle, count As Integer, isQuan As Boolean)
        ' Moi vien da la hinh ellipse nho
        Dim stoneD As Integer = If(isQuan, 14, 12)
        Dim pad As Integer = 6
        Dim innerW As Integer = r.Width - pad * 2
        Dim innerH As Integer = r.Height - pad * 2 - (If(isQuan, 18, 0))  ' bo cho chu QUAN
        Dim innerX As Integer = r.Left + pad
        Dim innerY As Integer = r.Top + pad + (If(isQuan, 18, 0))

        ' Tinh so cot / hang de xep
        Dim cols As Integer = Math.Max(1, innerW \ (stoneD + 2))
        Dim rows As Integer = Math.Max(1, innerH \ (stoneD + 2))
        Dim maxShow As Integer = cols * rows

        Dim shown As Integer = Math.Min(count, maxShow)

        Dim stoneColor As Color = Color.FromArgb(80, 60, 40)
        Dim stoneHighlight As Color = Color.FromArgb(150, 120, 80)

        Dim n As Integer = 0
        Dim row As Integer = 0
        Do While row < rows AndAlso n < shown
            Dim col As Integer = 0
            Do While col < cols AndAlso n < shown
                Dim sx As Integer = innerX + col * (stoneD + 2) + 1
                Dim sy As Integer = innerY + row * (stoneD + 2) + 1
                ' Bo' bong
                Using br As New SolidBrush(Color.FromArgb(60, 0, 0, 0))
                    g.FillEllipse(br, sx + 2, sy + 2, stoneD, stoneD)
                End Using
                ' Than vien da
                Using br As New SolidBrush(stoneColor)
                    g.FillEllipse(br, sx, sy, stoneD, stoneD)
                End Using
                ' Highlight nho goc tren trai
                Using br As New SolidBrush(stoneHighlight)
                    g.FillEllipse(br, sx + 2, sy + 2, stoneD \ 3, stoneD \ 3)
                End Using
                n += 1
                col += 1
            Loop
            row += 1
        Loop

        ' Neu con nhieu hon maxShow, hien so dem o goc phai duoi
        If count > maxShow Then
            Using fnt As New Font("Segoe UI", 7.5!, FontStyle.Bold)
                Using br As New SolidBrush(Color.DarkRed)
                    Dim txt As String = "+" & (count - maxShow).ToString()
                    g.DrawString(txt, fnt, br, r.Right - 22.0F, r.Bottom - 14.0F)
                End Using
            End Using
        End If

        ' Hien so dem nho o goc tren phai
        Using fnt As New Font("Segoe UI", 8.0!, FontStyle.Bold)
            Using br As New SolidBrush(Color.FromArgb(180, 50, 30, 10))
                g.DrawString(count.ToString(), fnt, br, CSng(r.Right - 16), CSng(r.Top + 3))
            End Using
        End Using
    End Sub

    ' ==========================================================
    '  MOUSE CLICK TREN BOARD
    ' ==========================================================
    Private Sub BoardPanel_MouseDown(sender As Object, e As MouseEventArgs)
        If animIsRunning Then Return
        If game Is Nothing OrElse game.GameOver Then Return
        If game.CurrentPlayer <> localPlayer Then
            AppendLog("Chua den luot ban.")
            Return
        End If

        ' Tim o duoc click
        Dim clicked As Integer = -1
        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
            If cellRect(i).Contains(e.Location) Then
                clicked = i
                Exit For
            End If
        Next i
        If clicked < 0 Then Return
        If game.OwnerOf(clicked) <> localPlayer Then Return
        If game.Stones(clicked) <= 0 Then Return

        selectedCell = clicked
        btnDirLeft.Enabled = True
        btnDirRight.Enabled = True
        boardPanel.Invalidate()
    End Sub

    ' ==========================================================
    '  ANIMATION
    ' ==========================================================
    Private Function CalcDropSequence(startIdx As Integer, hand As Integer, dir As OAQGame.GameDirection) As List(Of Integer)
        Dim seq As New List(Of Integer)()
        Dim cur As Integer = startIdx
        Dim h As Integer = hand
        Do While h > 0
            cur = NextCellIndex(cur, dir)
            seq.Add(cur)
            h -= 1
        Loop
        Return seq
    End Function

    Private Function NextCellIndex(idx As Integer, dir As OAQGame.GameDirection) As Integer
        If dir = OAQGame.GameDirection.CCW Then
            Return (idx + 1) Mod CELL_COUNT
        Else
            Return (idx + CELL_COUNT - 1) Mod CELL_COUNT
        End If
    End Function

    Private Sub StartAnimation(player As Integer, cellIdx As Integer, dir As OAQGame.GameDirection)
        animPendingPlayer = player
        animPendingCell = cellIdx
        animPendingDir = dir
        animSteps = CalcDropSequence(cellIdx, game.Stones(cellIdx), dir)
        animStep = 0
        animHighlightCell = -1
        animIsRunning = True
        btnDirLeft.Enabled = False
        btnDirRight.Enabled = False
        animTimer.Start()
    End Sub

    Private Sub AnimTimer_Tick(sender As Object, e As EventArgs)
        If animStep < animSteps.Count Then
            animHighlightCell = animSteps(animStep)
            animStep += 1
            boardPanel.Invalidate()
        Else
            animTimer.Stop()
            animIsRunning = False
            animHighlightCell = -1
            selectedCell = -1
            boardPanel.Invalidate()

            If isHost Then
                Dim errMsg As String = ""
                If game.TryMove(animPendingPlayer, animPendingCell, animPendingDir, errMsg) Then
                    RefreshBoardUI()
                    AppendLog(game.LastLog)
                    BroadcastState()
                    CheckAndShowGameOver()
                Else
                    AppendLog("Loi: " & errMsg)
                    RefreshBoardUI()
                End If
            Else
                RefreshBoardUI()
            End If
        End If
    End Sub

    ' ==========================================================
    '  KET NOI
    ' ==========================================================
    Private Sub BtnHost_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        isHost = True : localPlayer = -1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        Try
            peer.StartHost(port)
            lblStatus.Text = "Dang cho doi thu ket noi tren port " & port.ToString() & " ..."
        Catch ex As Exception
            MessageBox.Show("Khong the mo port: " & ex.Message)
        End Try
    End Sub

    Private Sub BtnJoin_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        If txtIP.Text.Trim().Length = 0 Then MessageBox.Show("Nhap IP cua Host.") : Return
        isHost = False : localPlayer = 1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        lblStatus.Text = "Dang ket noi den " & txtIP.Text.Trim() & ":" & port.ToString() & " ..."
        peer.ConnectToHost(txtIP.Text.Trim(), port)
    End Sub

    Private Sub Peer_Connected()
        If isHost Then
            lblStatus.Text = "Da co ket noi, dang cho HELLO tu khach..."
        Else
            peer.SendLine("HELLO:Client")
            lblStatus.Text = "Da ket noi den Host, dang khoi tao game..."
        End If
    End Sub

    Private Sub Peer_Disconnected()
        MessageBox.Show("Mat ket noi voi doi thu.")
        pnlGame.Visible = False : pnlConnect.Visible = True
        lblStatus.Text = "Da mat ket noi. Co the tao phong / vao phong lai."
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If line.StartsWith("HELLO") Then
            If isHost Then
                game = New OAQGame() : localPlayer = 0
                ShowGamePanel() : RefreshBoardUI() : BroadcastState()
                AppendLog("Doi thu da vao phong. Ban la Player 1 (Host), di truoc.")
            End If
        ElseIf line.StartsWith("STATE:") Then
            If game Is Nothing Then game = New OAQGame()
            game.Deserialize(line.Substring(6))
            If Not pnlGame.Visible Then ShowGamePanel()
            RefreshBoardUI()
            AppendLog(game.LastLog)
            CheckAndShowGameOver()
        ElseIf line.StartsWith("MOVEREQ:") Then
            If isHost Then HandleMoveRequest(line.Substring(8))
        ElseIf line.StartsWith("ERR:") Then
            AppendLog("Loi: " & line.Substring(4))
        End If
    End Sub

    Private Sub HandleMoveRequest(payload As String)
        Dim parts As String() = payload.Split(":"c)
        If parts.Length < 3 Then Return
        Dim p, si, d As Integer
        If Not Integer.TryParse(parts(0), p) Then Return
        If Not Integer.TryParse(parts(1), si) Then Return
        If Not Integer.TryParse(parts(2), d) Then Return
        Dim dir As OAQGame.GameDirection = If(d = 1, OAQGame.GameDirection.CW, OAQGame.GameDirection.CCW)
        StartAnimation(p, si, dir)
    End Sub

    Private Sub ShowGamePanel()
        pnlConnect.Visible = False : pnlGame.Visible = True
        lblYouAre.Text = "Ban la: " & If(localPlayer = 0, "Player 1 (Host)", "Player 2 (Khach)")
    End Sub

    Private Sub BroadcastState()
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("STATE:" & game.Serialize())
        End If
    End Sub

    ' ==========================================================
    '  NUT HUONG DI
    ' ==========================================================
    Private Sub BtnDirLeft_Click(sender As Object, e As EventArgs)
        ' P1 hang duoi: TRAI = CW; P2 hang tren: TRAI = CCW (dao nguoc)
        ExecuteMove(If(localPlayer = 0, OAQGame.GameDirection.CW, OAQGame.GameDirection.CCW))
    End Sub

    Private Sub BtnDirRight_Click(sender As Object, e As EventArgs)
        ' P1 hang duoi: PHAI = CCW; P2 hang tren: PHAI = CW
        ExecuteMove(If(localPlayer = 0, OAQGame.GameDirection.CCW, OAQGame.GameDirection.CW))
    End Sub

    Private Sub ExecuteMove(dir As OAQGame.GameDirection)
        If selectedCell = -1 OrElse animIsRunning Then Return
        btnDirLeft.Enabled = False : btnDirRight.Enabled = False
        If localPlayer = 0 Then
            StartAnimation(localPlayer, selectedCell, dir)
        Else
            Dim dCode As Integer = If(dir = OAQGame.GameDirection.CCW, 0, 1)
            peer.SendLine("MOVEREQ:" & localPlayer.ToString() & ":" & selectedCell.ToString() & ":" & dCode.ToString())
            StartAnimation(localPlayer, selectedCell, dir)
        End If
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As EventArgs)
        If Not isHost OrElse game Is Nothing Then AppendLog("Chi Host moi co the bat dau lai.") : Return
        If animIsRunning Then animTimer.Stop() : animIsRunning = False
        game.ResetBoard()
        selectedCell = -1 : animHighlightCell = -1
        RefreshBoardUI()
        AppendLog("Host da bat dau lai game.")
        BroadcastState()
    End Sub

    ' ==========================================================
    '  UPDATE UI
    ' ==========================================================
    Private Sub RefreshBoardUI()
        If game Is Nothing Then Return
        Dim myTurn As Boolean = (Not game.GameOver) AndAlso (game.CurrentPlayer = localPlayer)
        lblScore1.Text = "P1 (Host): " & game.Score(0).ToString() & " diem"
        lblScore2.Text = "P2 (Khach): " & game.Score(1).ToString() & " diem"
        If game.GameOver Then
            lblTurn.Text = "Da ket thuc"
        ElseIf myTurn Then
            lblTurn.Text = "Luot cua BAN - click o roi chon huong"
        Else
            lblTurn.Text = "Luot cua doi thu..."
        End If
        boardPanel.Invalidate()
    End Sub

    Private Sub CheckAndShowGameOver()
        If game IsNot Nothing AndAlso game.GameOver Then
            MessageBox.Show(game.LastLog, "Ket thuc tro choi")
        End If
    End Sub

    Private Sub AppendLog(msg As String)
        lstLog.Items.Add(msg)
        lstLog.TopIndex = lstLog.Items.Count - 1
    End Sub

End Class
