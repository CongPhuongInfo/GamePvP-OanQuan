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
    Private Const ANIM_DELAY_MS As Integer = 500

    Private game As OAQGame
    Private peer As NetworkPeer
    Private isHost As Boolean
    Private localPlayer As Integer = -1
    Private selectedCell As Integer = -1

    ' === UI connect ===
    Private pnlConnect As Panel
    Private txtPort As TextBox
    Private txtIP As TextBox
    Private btnHost As Button
    Private btnJoin As Button
    Private lblStatus As Label

    ' === UI game ===
    Private pnlGame As Panel
    Private boardPanel As Panel
    Private lblTurn As Label
    Private lblYouAre As Label
    Private btnDirLeft As Button
    Private btnDirRight As Button
    Private btnRestart As Button
    Private lstLog As ListBox
    ' Vung diem 2 nguoi
    Private pnlScore1 As Panel   ' Player1 goc trai duoi
    Private pnlScore2 As Panel   ' Player2 goc phai duoi

    ' === Board geometry ===
    ' Layout board (index):
    '   [Q0=0]  [11][10][9][8][7]  [Q1=6]   <- hang tren  (Player2)
    '   [Q0=0]  [ 1][ 2][3][4][5]  [Q1=6]   <- hang duoi (Player1)
    ' CCW = index tang: 0->1->2->3->4->5->6->7->8->9->10->11->0
    Private Const BW As Integer = 920
    Private Const BH As Integer = 320
    Private Const QUAN_W As Integer = 75
    Private Const DAN_W As Integer = 149
    Private Const DAN_H As Integer = 155
    Private Const QUAN_H As Integer = 314
    Private Const ROW_TOP As Integer = 3
    Private Const ROW_BOT As Integer = 160
    Private Const DAN_START_X As Integer = 81

    Private cellRect(CELL_COUNT - 1) As Rectangle

    ' === Animation state ===
    ' Moi buoc anim la 1 vien da roi vao 1 o
    ' animQueue: danh sach (cellIndex, eventType)
    '   eventType: 0=drop(+1 vien), 1=pickup(boc het o), 2=capture(an)
    Private Enum AnimEvent
        Drop = 0
        PickUp = 1
        Capture = 2
    End Enum
    Private Structure AnimStep
        Public CellIdx As Integer
        Public EvType As AnimEvent
        Public HandAfter As Integer   ' so vien tren tay sau buoc nay
    End Structure

    Private animQueue As New List(Of AnimStep)()
    Private animPos As Integer = 0
    Private animIsRunning As Boolean = False
    Private animTimer As System.Windows.Forms.Timer

    ' Trang thai hien thi trong animation (ban sao simulate)
    Private animSim(CELL_COUNT - 1) As Integer   ' so da hien thi tung o
    Private animHand As Integer = 0              ' so da tren tay
    Private animHandCell As Integer = -1         ' vi tri ban tay dang o (o cuoi vua rai/boc)
    Private animHighlight As Integer = -1        ' o dang nhan vien (flash)
    Private animScore1 As Integer = 0
    Private animScore2 As Integer = 0

    Private animPendingPlayer As Integer
    Private animPendingCell As Integer
    Private animPendingDir As OAQGame.GameDirection

    Public Sub New()
        BuildCellRects()
        InitUI()
    End Sub

    Private Sub BuildCellRects()
        ' Neu la Player2 (localPlayer=1): dao nguoc - hang minh (7..11) xuong duoi, hang P1 len tren
        ' Neu la Player1 hoac chua xac dinh: layout mac dinh - hang P1 (1..5) o duoi
        Dim myRowY As Integer
        Dim oppRowY As Integer
        Dim myStart As Integer   ' index bat dau hang minh
        Dim oppStart As Integer  ' index bat dau hang doi thu

        If localPlayer = 1 Then
            ' P2 nhin: hang minh (7..11) o duoi, hang P1 (1..5) o tren
            myRowY = ROW_BOT : oppRowY = ROW_TOP
            myStart = 7 : oppStart = 1
        Else
            ' P1 hoac default: hang P1 (1..5) o duoi, hang P2 (7..11) o tren
            myRowY = ROW_BOT : oppRowY = ROW_TOP
            myStart = 1 : oppStart = 7
        End If

        ' O Quan luon o 2 ben
        cellRect(0) = New Rectangle(3, ROW_TOP, QUAN_W, QUAN_H)
        cellRect(6) = New Rectangle(BW - QUAN_W - 3, ROW_TOP, QUAN_W, QUAN_H)

        ' Hang "duoi" (cua minh): tu trai sang phai
        Dim i As Integer
        For i = 0 To 4
            cellRect(myStart + i) = New Rectangle(DAN_START_X + i * (DAN_W + 3), myRowY, DAN_W, DAN_H)
        Next i

        ' Hang "tren" (doi thu): tu trai sang phai
        For i = 0 To 4
            cellRect(oppStart + i) = New Rectangle(DAN_START_X + i * (DAN_W + 3), oppRowY, DAN_W, DAN_H)
        Next i
    End Sub

    Private Sub InitUI()
        Me.Text = "O An Quan Online - 2CongLC"
        Me.ClientSize = New Size(BW, 680)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(240, 235, 215)

        animTimer = New System.Windows.Forms.Timer()
        animTimer.Interval = ANIM_DELAY_MS
        AddHandler animTimer.Tick, AddressOf AnimTimer_Tick

        BuildConnectPanel()
        BuildGamePanel()
        pnlGame.Visible = False
    End Sub

    ' ============================================================
    '  CONNECT PANEL
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Dock = DockStyle.Fill
        pnlConnect.BackColor = Color.FromArgb(240, 235, 215)

        Dim lbl As New Label()
        lbl.Text = "O AN QUAN ONLINE" : lbl.Font = New Font("Segoe UI", 18.0!, FontStyle.Bold)
        lbl.Location = New Point(280, 80) : lbl.AutoSize = True
        pnlConnect.Controls.Add(lbl)

        Dim lPort As New Label() : lPort.Text = "Port:" : lPort.Location = New Point(340, 160) : lPort.AutoSize = True
        pnlConnect.Controls.Add(lPort)
        txtPort = New TextBox() : txtPort.Text = DEFAULT_PORT.ToString() : txtPort.Location = New Point(400, 157) : txtPort.Width = 80
        pnlConnect.Controls.Add(txtPort)

        btnHost = New Button() : btnHost.Text = "Tao phong (Host)"
        btnHost.Location = New Point(340, 200) : btnHost.Size = New Size(200, 38)
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        Dim lIP As New Label() : lIP.Text = "IP Host:" : lIP.Location = New Point(340, 260) : lIP.AutoSize = True
        pnlConnect.Controls.Add(lIP)
        txtIP = New TextBox() : txtIP.Text = "127.0.0.1" : txtIP.Location = New Point(410, 257) : txtIP.Width = 140
        pnlConnect.Controls.Add(txtIP)

        btnJoin = New Button() : btnJoin.Text = "Vao phong (Join)"
        btnJoin.Location = New Point(340, 295) : btnJoin.Size = New Size(200, 38)
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lblStatus = New Label() : lblStatus.Location = New Point(280, 370) : lblStatus.AutoSize = True
        lblStatus.ForeColor = Color.DimGray
        lblStatus.Text = "Host: bam 'Tao phong'." & Environment.NewLine & "Khach: nhap IP roi bam 'Vao phong'."
        pnlConnect.Controls.Add(lblStatus)

        Me.Controls.Add(pnlConnect)
    End Sub

    ' ============================================================
    '  GAME PANEL
    ' ============================================================
    Private Sub BuildGamePanel()
        pnlGame = New Panel()
        pnlGame.Location = New Point(0, 0)
        pnlGame.Size = New Size(BW, 680)
        pnlGame.BackColor = Color.FromArgb(240, 235, 215)

        ' Header
        lblTurn = New Label() : lblTurn.Location = New Point(10, 8) : lblTurn.AutoSize = True
        lblTurn.Font = New Font("Segoe UI", 11.0!, FontStyle.Bold)
        pnlGame.Controls.Add(lblTurn)

        lblYouAre = New Label() : lblYouAre.Location = New Point(10, 32) : lblYouAre.AutoSize = True
        pnlGame.Controls.Add(lblYouAre)

        ' Board GDI
        boardPanel = New Panel()
        boardPanel.Location = New Point(0, 55)
        boardPanel.Size = New Size(BW, BH)
        boardPanel.BackColor = Color.FromArgb(185, 145, 80)
        boardPanel.Cursor = Cursors.Hand
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        AddHandler boardPanel.MouseDown, AddressOf BoardPanel_MouseDown
        pnlGame.Controls.Add(boardPanel)

        ' Vung diem Player1 (goc trai)
        pnlScore1 = New Panel()
        pnlScore1.Location = New Point(10, 55 + BH + 10)
        pnlScore1.Size = New Size(300, 120)
        pnlScore1.BackColor = Color.FromArgb(200, 225, 255)
        pnlScore1.BorderStyle = BorderStyle.FixedSingle
        AddHandler pnlScore1.Paint, AddressOf PnlScore1_Paint
        pnlGame.Controls.Add(pnlScore1)

        ' Vung diem Player2 (goc phai)
        pnlScore2 = New Panel()
        pnlScore2.Location = New Point(BW - 310, 55 + BH + 10)
        pnlScore2.Size = New Size(300, 120)
        pnlScore2.BackColor = Color.FromArgb(200, 255, 210)
        pnlScore2.BorderStyle = BorderStyle.FixedSingle
        AddHandler pnlScore2.Paint, AddressOf PnlScore2_Paint
        pnlGame.Controls.Add(pnlScore2)

        Dim btnY As Integer = 55 + BH + 140
        btnDirLeft = New Button() : btnDirLeft.Text = "Di TRAI"
        btnDirLeft.Location = New Point(310, btnY) : btnDirLeft.Size = New Size(130, 36) : btnDirLeft.Enabled = False
        AddHandler btnDirLeft.Click, AddressOf BtnDirLeft_Click
        pnlGame.Controls.Add(btnDirLeft)

        btnDirRight = New Button() : btnDirRight.Text = "Di PHAI"
        btnDirRight.Location = New Point(460, btnY) : btnDirRight.Size = New Size(130, 36) : btnDirRight.Enabled = False
        AddHandler btnDirRight.Click, AddressOf BtnDirRight_Click
        pnlGame.Controls.Add(btnDirRight)

        btnRestart = New Button() : btnRestart.Text = "Choi lai (Host)"
        btnRestart.Location = New Point(640, btnY) : btnRestart.Size = New Size(150, 36)
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        pnlGame.Controls.Add(btnRestart)

        lstLog = New ListBox()
        lstLog.Location = New Point(10, btnY + 46)
        lstLog.Size = New Size(BW - 20, 100)
        pnlGame.Controls.Add(lstLog)

        Me.Controls.Add(pnlGame)
    End Sub

    ' ============================================================
    '  GDI BOARD
    ' ============================================================
    Private Sub BoardPanel_Paint(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
            DrawCell(g, i)
        Next i

        ' Ve ban tay + so vien dang cam
        If animIsRunning AndAlso animHandCell >= 0 AndAlso animHand > 0 Then
            DrawHand(g, animHandCell, animHand)
        End If
    End Sub

    Private Sub DrawCell(g As Graphics, idx As Integer)
        Dim r As Rectangle = cellRect(idx)
        Dim isQuan As Boolean = (game IsNot Nothing AndAlso game.IsQuan(idx))
        Dim isSelected As Boolean = (idx = selectedCell)
        Dim isHL As Boolean = (idx = animHighlight)

        ' Mau nen
        Dim fill As Color
        If isHL Then
            fill = Color.FromArgb(255, 235, 60)
        ElseIf isSelected Then
            fill = Color.FromArgb(130, 200, 130)
        ElseIf isQuan Then
            fill = Color.FromArgb(190, 140, 55)
        ElseIf game IsNot Nothing AndAlso game.OwnerOf(idx) = 0 Then
            fill = Color.FromArgb(175, 205, 240)
        ElseIf game IsNot Nothing AndAlso game.OwnerOf(idx) = 1 Then
            fill = Color.FromArgb(175, 230, 180)
        Else
            fill = Color.FromArgb(195, 170, 115)
        End If

        Using br As New SolidBrush(fill)
            g.FillRectangle(br, r)
        End Using

        Dim bw As Integer = If(isSelected OrElse isHL, 3, 1)
        Dim bc As Color = If(isSelected, Color.DarkGreen, If(isHL, Color.OrangeRed, Color.FromArgb(100, 75, 30)))
        Using p As New Pen(bc, bw)
            g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1)
        End Using

        ' Ten o Quan
        If isQuan Then
            Using fnt As New Font("Segoe UI", 9.0!, FontStyle.Bold)
            Using br As New SolidBrush(Color.DarkRed)
                Dim s As String = "QUAN"
                Dim sz As SizeF = g.MeasureString(s, fnt)
                g.DrawString(s, fnt, br, r.Left + (r.Width - sz.Width) / 2.0F, r.Top + 4.0F)
            End Using
            End Using
        End If

        ' So vien tu animSim hoac game.Stones
        Dim stones As Integer = 0
        If animIsRunning Then
            stones = animSim(idx)
        ElseIf game IsNot Nothing Then
            stones = game.Stones(idx)
        End If

        If stones > 0 Then
            DrawStones(g, r, stones, isQuan)
        End If
    End Sub

    Private Sub DrawStones(g As Graphics, r As Rectangle, count As Integer, isQuan As Boolean)
        Dim sd As Integer = If(isQuan, 13, 11)
        Dim pad As Integer = 5
        Dim topOff As Integer = If(isQuan, 20, 0)
        Dim iw As Integer = r.Width - pad * 2
        Dim ih As Integer = r.Height - pad * 2 - topOff
        Dim ix As Integer = r.Left + pad
        Dim iy As Integer = r.Top + pad + topOff

        Dim cols As Integer = Math.Max(1, iw \ (sd + 2))
        Dim rows As Integer = Math.Max(1, ih \ (sd + 2))
        Dim maxShow As Integer = cols * rows
        Dim shown As Integer = Math.Min(count, maxShow)

        Dim n As Integer = 0
        Dim row As Integer = 0
        Do While row < rows AndAlso n < shown
            Dim col As Integer = 0
            Do While col < cols AndAlso n < shown
                Dim sx As Integer = ix + col * (sd + 2)
                Dim sy As Integer = iy + row * (sd + 2)
                Using br As New SolidBrush(Color.FromArgb(50, 0, 0, 0))
                    g.FillEllipse(br, sx + 2, sy + 2, sd, sd)
                End Using
                Using br As New SolidBrush(Color.FromArgb(70, 52, 30))
                    g.FillEllipse(br, sx, sy, sd, sd)
                End Using
                Using br As New SolidBrush(Color.FromArgb(140, 110, 70))
                    g.FillEllipse(br, sx + 2, sy + 1, sd \ 3, sd \ 3)
                End Using
                n += 1 : col += 1
            Loop
            row += 1
        Loop

        If count > maxShow Then
            Using fnt As New Font("Segoe UI", 7.0!, FontStyle.Bold)
            Using br As New SolidBrush(Color.DarkRed)
                g.DrawString("+" & (count - maxShow).ToString(), fnt, br, CSng(r.Right - 20), CSng(r.Bottom - 14))
            End Using
            End Using
        End If

        ' So dem goc tren phai
        Using fnt As New Font("Segoe UI", 8.5!, FontStyle.Bold)
        Using br As New SolidBrush(Color.FromArgb(200, 80, 40, 10))
            g.DrawString(count.ToString(), fnt, br, CSng(r.Right - 18), CSng(r.Top + 2))
        End Using
        End Using
    End Sub

    ' Ve bieu tuong ban tay + so vien dang cam
    Private Sub DrawHand(g As Graphics, cellIdx As Integer, handCount As Integer)
        Dim r As Rectangle = cellRect(cellIdx)
        Dim cx As Integer = r.Left + r.Width \ 2
        Dim cy As Integer = r.Top + r.Height \ 2

        ' Ve icon ban tay don gian bang cac hinh chu nhat (ngu ngon)
        Dim hx As Integer = cx - 16
        Dim hy As Integer = cy - 20
        Dim fingerW As Integer = 8
        Dim palmH As Integer = 18

        ' 4 ngon tay
        Dim heights() As Integer = {28, 32, 30, 26}
        Dim fi As Integer
        For fi = 0 To 3
            Dim fx As Integer = hx + fi * (fingerW + 2)
            Dim fh As Integer = heights(fi)
            Using br As New SolidBrush(Color.FromArgb(230, 200, 160))
                g.FillRectangle(br, fx, hy - fh + palmH, fingerW, fh)
            End Using
            Using p As New Pen(Color.FromArgb(150, 120, 80), 1)
                g.DrawRectangle(p, fx, hy - fh + palmH, fingerW, fh)
            End Using
        Next fi

        ' Long ban tay
        Using br As New SolidBrush(Color.FromArgb(230, 200, 160))
            g.FillRectangle(br, hx - 2, hy, 4 * fingerW + 3 * 2 + 4, palmH)
        End Using
        Using p As New Pen(Color.FromArgb(150, 120, 80), 1)
            g.DrawRectangle(p, hx - 2, hy, 4 * fingerW + 3 * 2 + 4, palmH)
        End Using

        ' So vien dang cam - hien trong hinh tron nho ben canh ban tay
        Dim bx As Integer = cx + 20
        Dim by As Integer = cy - 22
        Using br As New SolidBrush(Color.FromArgb(220, 60, 60))
            g.FillEllipse(br, bx, by, 24, 24)
        End Using
        Using br As New SolidBrush(Color.White)
        Using fnt As New Font("Segoe UI", 9.0!, FontStyle.Bold)
            Dim txt As String = handCount.ToString()
            Dim sz As SizeF = g.MeasureString(txt, fnt)
            g.DrawString(txt, fnt, br, bx + (24 - sz.Width) / 2.0F, by + (24 - sz.Height) / 2.0F)
        End Using
        End Using
    End Sub

    ' ============================================================
    '  SCORE PANELS
    ' ============================================================
    Private Sub PnlScore1_Paint(sender As Object, e As PaintEventArgs)
        DrawScorePanel(e.Graphics, 0, pnlScore1.ClientRectangle)
    End Sub

    Private Sub PnlScore2_Paint(sender As Object, e As PaintEventArgs)
        DrawScorePanel(e.Graphics, 1, pnlScore2.ClientRectangle)
    End Sub

    Private Sub DrawScorePanel(g As Graphics, player As Integer, r As Rectangle)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim sc As Integer = If(animIsRunning, If(player = 0, animScore1, animScore2), If(game IsNot Nothing, game.Score(player), 0))
        Dim name As String = If(player = 0, "Player 1 (Host)", "Player 2 (Khach)")

        Using fnt As New Font("Segoe UI", 10.0!, FontStyle.Bold)
        Using br As New SolidBrush(Color.FromArgb(50, 50, 80))
            g.DrawString(name, fnt, br, 8.0F, 6.0F)
        End Using
        End Using

        Using fnt As New Font("Segoe UI", 22.0!, FontStyle.Bold)
        Using br As New SolidBrush(Color.FromArgb(180, 40, 40))
            g.DrawString(sc.ToString() & " diem", fnt, br, 8.0F, 28.0F)
        End Using
        End Using

        ' Ve cac vien da an duoc (toi da 30 vien hien thi)
        Dim shown As Integer = Math.Min(sc, 30)
        Dim sd As Integer = 10
        Dim px As Integer = 8 : Dim py As Integer = 78
        Dim n As Integer
        For n = 0 To shown - 1
            Dim sx As Integer = px + n * (sd + 2)
            If sx + sd > r.Width - 5 Then Exit For
            Using br As New SolidBrush(Color.FromArgb(70, 52, 30))
                g.FillEllipse(br, sx, py, sd, sd)
            End Using
            Using br As New SolidBrush(Color.FromArgb(140, 110, 70))
                g.FillEllipse(br, sx + 2, py + 1, sd \ 3, sd \ 3)
            End Using
        Next n
        If sc > 30 Then
            Using fnt As New Font("Segoe UI", 8.0!, FontStyle.Bold)
            Using br As New SolidBrush(Color.DarkRed)
                g.DrawString("+" & (sc - 30).ToString(), fnt, br, CSng(r.Width - 30), CSng(py))
            End Using
            End Using
        End If
    End Sub

    ' ============================================================
    '  CLICK TREN BOARD
    ' ============================================================
    Private Sub BoardPanel_MouseDown(sender As Object, e As MouseEventArgs)
        If animIsRunning Then Return
        If game Is Nothing OrElse game.GameOver Then Return
        If game.CurrentPlayer <> localPlayer Then
            AppendLog("Chua den luot ban.")
            Return
        End If

        Dim clicked As Integer = -1
        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
            If cellRect(i).Contains(e.Location) Then clicked = i : Exit For
        Next i
        If clicked < 0 Then Return
        If game.OwnerOf(clicked) <> localPlayer Then Return
        If game.Stones(clicked) <= 0 Then Return

        selectedCell = clicked
        btnDirLeft.Enabled = True
        btnDirRight.Enabled = True
        boardPanel.Invalidate()
    End Sub

    ' ============================================================
    '  HUONG DI
    ' ============================================================
    Private Sub BtnDirLeft_Click(sender As Object, e As EventArgs)
        ExecuteMove(If(localPlayer = 0, OAQGame.GameDirection.CW, OAQGame.GameDirection.CCW))
    End Sub

    Private Sub BtnDirRight_Click(sender As Object, e As EventArgs)
        ExecuteMove(If(localPlayer = 0, OAQGame.GameDirection.CCW, OAQGame.GameDirection.CW))
    End Sub

    Private Sub ExecuteMove(dir As OAQGame.GameDirection)
        If selectedCell = -1 OrElse animIsRunning Then Return
        btnDirLeft.Enabled = False : btnDirRight.Enabled = False

        If localPlayer <> 0 Then
            Dim dCode As Integer = If(dir = OAQGame.GameDirection.CCW, 0, 1)
            peer.SendLine("MOVEREQ:" & localPlayer.ToString() & ":" & selectedCell.ToString() & ":" & dCode.ToString())
        End If

        StartAnimation(localPlayer, selectedCell, dir)
    End Sub

    ' ============================================================
    '  BUILD ANIMATION QUEUE
    '  Simulate toan bo nuoc di, tao danh sach AnimStep
    ' ============================================================
    Private Function BuildAnimQueue(startIdx As Integer, dir As OAQGame.GameDirection) As List(Of AnimStep)
        Dim q As New List(Of AnimStep)()

        ' Sao chep board
        Dim sim(11) As Integer
        Dim i As Integer
        For i = 0 To 11
            sim(i) = game.Stones(i)
        Next i
        Dim sc1 As Integer = game.Score(0)
        Dim sc2 As Integer = game.Score(1)

        Dim hand As Integer = sim(startIdx)
        sim(startIdx) = 0

        ' Buoc dau: boc o xuat phat
        Dim st0 As AnimStep
        st0.CellIdx = startIdx
        st0.EvType = AnimEvent.PickUp
        st0.HandAfter = hand
        q.Add(st0)

        Dim cur As Integer = startIdx
        Dim guard As Integer = 0
        Dim distributing As Boolean = True

        Do While distributing
            guard += 1
            If guard > 10000 Then Exit Do

            ' Rai tung vien
            Do While hand > 0
                cur = NextIdx(cur, dir)
                sim(cur) += 1
                hand -= 1
                Dim st As AnimStep
                st.CellIdx = cur
                st.EvType = AnimEvent.Drop
                st.HandAfter = hand
                q.Add(st)
            Loop

            ' Kiem tra o ke tiep
            Dim peek As Integer = NextIdx(cur, dir)
            If sim(peek) > 0 Then
                ' Boc tiep
                hand = sim(peek)
                sim(peek) = 0
                cur = peek
                Dim stP As AnimStep
                stP.CellIdx = cur
                stP.EvType = AnimEvent.PickUp
                stP.HandAfter = hand
                q.Add(stP)
            Else
                ' Pha an: o tiep theo rong, kiem tra o ke nua
                Dim check As Integer = NextIdx(peek, dir)
                Dim eating As Boolean = True
                Do While eating
                    guard += 1
                    If guard > 10000 Then Exit Do
                    If check = startIdx Then
                        eating = False
                    ElseIf sim(check) > 0 Then
                        ' An o check
                        Dim stC As AnimStep
                        stC.CellIdx = check
                        stC.EvType = AnimEvent.Capture
                        stC.HandAfter = 0
                        If game.OwnerOf(check) = 0 OrElse game.IsQuan(check) Then
                            sc1 += sim(check)
                        Else
                            sc1 += 0
                            sc2 += sim(check)
                        End If
                        ' An ve cho dung player
                        sim(check) = 0
                        q.Add(stC)
                        Dim after As Integer = NextIdx(check, dir)
                        If after = startIdx OrElse sim(after) > 0 Then
                            eating = False
                        Else
                            check = NextIdx(after, dir)
                        End If
                    Else
                        eating = False
                    End If
                Loop
                distributing = False
            End If
        Loop

        Return q
    End Function

    Private Function NextIdx(idx As Integer, dir As OAQGame.GameDirection) As Integer
        If dir = OAQGame.GameDirection.CCW Then
            Return (idx + 1) Mod CELL_COUNT
        Else
            Return (idx + CELL_COUNT - 1) Mod CELL_COUNT
        End If
    End Function

    ' ============================================================
    '  CHAY ANIMATION
    ' ============================================================
    Private Sub StartAnimation(player As Integer, cellIdx As Integer, dir As OAQGame.GameDirection)
        animPendingPlayer = player
        animPendingCell = cellIdx
        animPendingDir = dir

        animQueue = BuildAnimQueue(cellIdx, dir)
        animPos = 0
        animIsRunning = True
        animHighlight = -1

        ' Khoi tao animSim = game hien tai
        Dim i As Integer
        For i = 0 To 11
            animSim(i) = game.Stones(i)
        Next i
        animHand = 0
        animHandCell = cellIdx
        animScore1 = game.Score(0)
        animScore2 = game.Score(1)

        selectedCell = -1
        boardPanel.Invalidate()
        animTimer.Start()
    End Sub

    Private Sub AnimTimer_Tick(sender As Object, e As EventArgs)
        If animPos >= animQueue.Count Then
            ' Xong animation
            animTimer.Stop()
            animIsRunning = False
            animHighlight = -1
            animHand = 0
            animHandCell = -1

            If isHost Then
                Dim errMsg As String = ""
                If game.TryMove(animPendingPlayer, animPendingCell, animPendingDir, errMsg) Then
                    AppendLog(game.LastLog)
                    BroadcastState()
                    CheckAndShowGameOver()
                Else
                    AppendLog("Loi: " & errMsg)
                End If
            End If

            RefreshUI()
            Return
        End If

        Dim step As AnimStep = animQueue(animPos)
        animPos += 1

        Select Case step.EvType
            Case AnimEvent.Drop
                animSim(step.CellIdx) += 1   ' vien roi vao o
                animHand = step.HandAfter
                animHandCell = step.CellIdx
                animHighlight = step.CellIdx

            Case AnimEvent.PickUp
                animSim(step.CellIdx) = 0    ' boc het o
                animHand = step.HandAfter
                animHandCell = step.CellIdx
                animHighlight = step.CellIdx

            Case AnimEvent.Capture
                animSim(step.CellIdx) = 0    ' an sach o
                ' cap nhat diem hien thi
                animScore1 = game.Score(0)
                animScore2 = game.Score(1)
                animHighlight = step.CellIdx
        End Select

        boardPanel.Invalidate()
        pnlScore1.Invalidate()
        pnlScore2.Invalidate()
    End Sub

    ' ============================================================
    '  KET NOI MANG
    ' ============================================================
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
            lblStatus.Text = "Dang cho doi thu tren port " & port.ToString() & "..."
        Catch ex As Exception
            MessageBox.Show("Loi: " & ex.Message)
        End Try
    End Sub

    Private Sub BtnJoin_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        If txtIP.Text.Trim() = "" Then MessageBox.Show("Nhap IP.") : Return
        isHost = False : localPlayer = 1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        lblStatus.Text = "Dang ket noi..."
        peer.ConnectToHost(txtIP.Text.Trim(), port)
    End Sub

    Private Sub Peer_Connected()
        If Not isHost Then peer.SendLine("HELLO:Client")
    End Sub

    Private Sub Peer_Disconnected()
        MessageBox.Show("Mat ket noi.")
        pnlGame.Visible = False : pnlConnect.Visible = True
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If line.StartsWith("HELLO") Then
            If isHost Then
                game = New OAQGame() : localPlayer = 0
                ShowGamePanel() : BroadcastState()
                AppendLog("Doi thu vao phong. Ban la Player 1, di truoc.")
            End If
        ElseIf line.StartsWith("STATE:") Then
            If game Is Nothing Then game = New OAQGame()
            game.Deserialize(line.Substring(6))
            If Not pnlGame.Visible Then ShowGamePanel()
            RefreshUI()
            AppendLog(game.LastLog)
            CheckAndShowGameOver()
        ElseIf line.StartsWith("MOVEREQ:") Then
            If isHost Then
                Dim parts As String() = line.Substring(8).Split(":"c)
                If parts.Length >= 3 Then
                    Dim p, si, d As Integer
                    Integer.TryParse(parts(0), p)
                    Integer.TryParse(parts(1), si)
                    Integer.TryParse(parts(2), d)
                    Dim dir As OAQGame.GameDirection = If(d = 1, OAQGame.GameDirection.CW, OAQGame.GameDirection.CCW)
                    StartAnimation(p, si, dir)
                End If
            End If
        End If
    End Sub

    Private Sub ShowGamePanel()
        pnlConnect.Visible = False : pnlGame.Visible = True
        lblYouAre.Text = "Ban la: " & If(localPlayer = 0, "Player 1 (Host)", "Player 2 (Khach)")
        BuildCellRects()   ' rebuild layout theo localPlayer
        RefreshUI()
    End Sub

    Private Sub BroadcastState()
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("STATE:" & game.Serialize())
        End If
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As EventArgs)
        If Not isHost OrElse game Is Nothing Then Return
        If animIsRunning Then animTimer.Stop() : animIsRunning = False
        game.ResetBoard()
        selectedCell = -1
        RefreshUI()
        AppendLog("Bat dau lai.")
        BroadcastState()
    End Sub

    Private Sub RefreshUI()
        If game Is Nothing Then Return
        Dim myTurn As Boolean = (Not game.GameOver) AndAlso (game.CurrentPlayer = localPlayer)
        If game.GameOver Then
            lblTurn.Text = "Ket thuc!"
        ElseIf myTurn Then
            lblTurn.Text = "Luot cua BAN - click o roi chon huong"
        Else
            lblTurn.Text = "Luot cua doi thu..."
        End If
        boardPanel.Invalidate()
        pnlScore1.Invalidate()
        pnlScore2.Invalidate()
    End Sub

    Private Sub CheckAndShowGameOver()
        If game IsNot Nothing AndAlso game.GameOver Then
            MessageBox.Show(game.LastLog, "Ket thuc!")
        End If
    End Sub

    Private Sub AppendLog(msg As String)
        lstLog.Items.Add(msg)
        lstLog.TopIndex = lstLog.Items.Count - 1
    End Sub

End Class
