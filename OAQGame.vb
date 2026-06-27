Option Strict On
Option Explicit On

Imports System.Text

''' <summary>
''' Logic loi choi O An Quan, don gian hoa cho ban PvP online.
''' Board la mot vong 12 o: index 0 = Quan trai, index 1..5 = hang Player1 (Host),
''' index 6 = Quan phai, index 7..11 = hang Player2 (Khach).
''' Quy uoc don gian hoa (ghi ro de ban tien chinh sua neu can):
'''  - Moi o Quan bat dau co 10 "diem" (tuong duong gia tri Quan), 10 o dan bat dau co 5 quan.
'''  - Di quan: boc het quan trong 1 o cua minh, rai lien tiep 1 quan/o theo huong da chon.
'''  - Het quan tren tay, neu o ke tiep co quan -> boc tiep va rai tiep.
'''  - Neu o ke tiep rong -> kiem tra o sau do: co quan thi "an" (cham diem), roi kiem tra
'''    tiep o sau o vua an (chuoi an lien tuc neu cu xen rong-co quan).
'''  - Het luot khi gap o co quan ma khong roi vao truong hop boc tiep / khi het chuoi an.
'''  - Het dan: khi toan bo 10 o dan = 0, ket thuc; Quan con lai (neu chua bi an) duoc cong
'''    cho phia tuong ung (Quan trai -> Player1, Quan phai -> Player2).
'''  - Vay quan: neu den luot ma toan bo hang cua minh = 0 nhung doi phuong con, tu dong
'''    vay 1 quan tu o gan nhat cua doi phuong va tru 1 diem (no) cua nguoi vay.
''' </summary>
Public Class OAQGame

    Public Enum GameDirection
        CCW = 0 ' nguoc chieu kim dong ho: index tang
        CW = 1  ' cung chieu kim dong ho: index giam
    End Enum

    Private Const CELL_COUNT As Integer = 12
    Private Const QUAN0 As Integer = 0
    Private Const QUAN1 As Integer = 6
    Private Const MAX_LOOP_GUARD As Integer = 20000

    Public Stones(CELL_COUNT - 1) As Integer
    Public Score(1) As Integer ' Score(0) = Player1 (Host), Score(1) = Player2 (Khach)
    Public CurrentPlayer As Integer
    Public GameOver As Boolean
    Public LastLog As String

    Public Sub New()
        ResetBoard()
    End Sub

    Public Sub ResetBoard()
        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
            If i = QUAN0 OrElse i = QUAN1 Then
                Stones(i) = 10
            Else
                Stones(i) = 5
            End If
        Next i
        Score(0) = 0
        Score(1) = 0
        CurrentPlayer = 0
        GameOver = False
        LastLog = "Bat dau game moi."
    End Sub

    Public Function IsQuan(cellIndex As Integer) As Boolean
        Return cellIndex = QUAN0 OrElse cellIndex = QUAN1
    End Function

    ''' <summary>Tra ve 0 (Player1/Host), 1 (Player2/Khach), hoac -1 voi o Quan.</summary>
    Public Function OwnerOf(cellIndex As Integer) As Integer
        If cellIndex = QUAN0 OrElse cellIndex = QUAN1 Then
            Return -1
        ElseIf cellIndex >= 1 AndAlso cellIndex <= 5 Then
            Return 0
        Else
            Return 1
        End If
    End Function

    Private Function NextIndex(idx As Integer, dir As GameDirection) As Integer
        If dir = GameDirection.CCW Then
            Return (idx + 1) Mod CELL_COUNT
        Else
            Return (idx + CELL_COUNT - 1) Mod CELL_COUNT
        End If
    End Function

    Public Function HasMovableCells(player As Integer) As Boolean
        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
            If OwnerOf(i) = player AndAlso Stones(i) > 0 Then
                Return True
            End If
        Next i
        Return False
    End Function

    Public Sub HandleBorrowIfNeeded(player As Integer)
        If GameOver Then Return
        If HasMovableCells(player) Then Return

        Dim opponent As Integer = 1 - player
        Dim oppFirstIndex As Integer = If(opponent = 0, 1, 7)
        Dim myFirstIndex As Integer = If(player = 0, 1, 7)
        Dim i As Integer
        Dim borrowed As Boolean = False

        For i = 0 To 4
            Dim oi As Integer = oppFirstIndex + i
            If Stones(oi) > 0 Then
                Stones(oi) -= 1
                Stones(myFirstIndex) += 1
                Score(player) -= 1
                borrowed = True
                Exit For
            End If
        Next i

        If borrowed Then
            LastLog = String.Format("Player {0} het quan, da vay 1 hat (-1 diem).", player + 1)
        End If
    End Sub

    Private Sub CheckGameEnd()
        Dim i As Integer
        Dim anyDan As Boolean = False
        For i = 0 To CELL_COUNT - 1
            If Not IsQuan(i) AndAlso Stones(i) > 0 Then
                anyDan = True
                Exit For
            End If
        Next i
        If Not anyDan Then
            EndGameAndTally()
        End If
    End Sub

    Private Sub EndGameAndTally()
        If GameOver Then Return

        If Stones(QUAN0) > 0 Then
            Score(0) += Stones(QUAN0)
            Stones(QUAN0) = 0
        End If
        If Stones(QUAN1) > 0 Then
            Score(1) += Stones(QUAN1)
            Stones(QUAN1) = 0
        End If

        GameOver = True

        If Score(0) > Score(1) Then
            LastLog = "Ket thuc! Player 1 (Host) thang " & Score(0).ToString() & " - " & Score(1).ToString()
        ElseIf Score(1) > Score(0) Then
            LastLog = "Ket thuc! Player 2 (Khach) thang " & Score(1).ToString() & " - " & Score(0).ToString()
        Else
            LastLog = "Ket thuc! Hoa " & Score(0).ToString() & " - " & Score(1).ToString()
        End If
    End Sub

    ''' <summary>Thuc hien mot luoc di. Tra ve True neu hop le va da thuc hien.</summary>
    Public Function TryMove(player As Integer, startIndex As Integer, dir As GameDirection, ByRef errorMsg As String) As Boolean
        errorMsg = ""

        If GameOver Then
            errorMsg = "Game da ket thuc."
            Return False
        End If
        If player <> CurrentPlayer Then
            errorMsg = "Khong phai luot cua ban."
            Return False
        End If
        If startIndex < 0 OrElse startIndex >= CELL_COUNT Then
            errorMsg = "O khong hop le."
            Return False
        End If
        If OwnerOf(startIndex) <> player Then
            errorMsg = "Ban chi duoc chon o thuoc hang cua minh."
            Return False
        End If
        If Stones(startIndex) <= 0 Then
            errorMsg = "O nay khong co quan."
            Return False
        End If

        Dim hand As Integer = Stones(startIndex)
        Stones(startIndex) = 0
        Dim cur As Integer = startIndex
        Dim captured As Integer = 0
        Dim distributing As Boolean = True
        Dim guard As Integer = 0

        Do While distributing
            guard += 1
            If guard > MAX_LOOP_GUARD Then
                errorMsg = "Loi noi bo: vong lap qua dai, huy luoc di."
                Return False
            End If

            Do While hand > 0
                cur = NextIndex(cur, dir)
                Stones(cur) += 1
                hand -= 1
            Loop

            Dim peekCell As Integer = NextIndex(cur, dir)
            If Stones(peekCell) > 0 Then
                hand = Stones(peekCell)
                Stones(peekCell) = 0
                cur = peekCell
            Else
                Dim checkCell As Integer = NextIndex(peekCell, dir)
                Dim capturing As Boolean = True
                Do While capturing
                    guard += 1
                    If guard > MAX_LOOP_GUARD Then
                        errorMsg = "Loi noi bo: vong lap an quan qua dai."
                        Return False
                    End If

                    If checkCell = startIndex Then
                        capturing = False
                    ElseIf Stones(checkCell) > 0 Then
                        captured += Stones(checkCell)
                        Stones(checkCell) = 0
                        Dim afterCell As Integer = NextIndex(checkCell, dir)
                        If afterCell = startIndex OrElse Stones(afterCell) > 0 Then
                            capturing = False
                        Else
                            checkCell = NextIndex(afterCell, dir)
                        End If
                    Else
                        capturing = False
                    End If
                Loop
                distributing = False
            End If
        Loop

        Score(player) += captured

        If captured > 0 Then
            LastLog = String.Format("Player {0} di tu o {1}, an duoc {2} quan.", player + 1, startIndex, captured)
        Else
            LastLog = String.Format("Player {0} di tu o {1}, khong an duoc quan nao.", player + 1, startIndex)
        End If

        CheckGameEnd()

        If Not GameOver Then
            CurrentPlayer = 1 - player
            HandleBorrowIfNeeded(CurrentPlayer)
            If Not HasMovableCells(CurrentPlayer) Then
                EndGameAndTally()
            End If
        End If

        Return True
    End Function

    Public Function Serialize() As String
        Dim sb As New StringBuilder()
        Dim i As Integer

        For i = 0 To CELL_COUNT - 1
            sb.Append(Stones(i).ToString())
            If i < CELL_COUNT - 1 Then sb.Append(",")
        Next i

        sb.Append("|")
        sb.Append(Score(0).ToString())
        sb.Append("|")
        sb.Append(Score(1).ToString())
        sb.Append("|")
        sb.Append(CurrentPlayer.ToString())
        sb.Append("|")
        sb.Append(If(GameOver, "1", "0"))
        sb.Append("|")
        sb.Append(LastLog.Replace("|", " ").Replace(Chr(13), " ").Replace(Chr(10), " "))

        Return sb.ToString()
    End Function

    Public Sub Deserialize(data As String)
        Dim parts As String() = data.Split("|"c)
        Dim stoneParts As String() = parts(0).Split(","c)
        Dim i As Integer

        For i = 0 To CELL_COUNT - 1
            Stones(i) = Integer.Parse(stoneParts(i))
        Next i

        Score(0) = Integer.Parse(parts(1))
        Score(1) = Integer.Parse(parts(2))
        CurrentPlayer = Integer.Parse(parts(3))
        GameOver = (parts(4) = "1")

        If parts.Length >= 6 Then
            LastLog = parts(5)
        End If
    End Sub

End Class
