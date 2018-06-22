Imports System.Collections.Generic
Imports System.Linq

Namespace Data.Schedule
    Public Class EventMatch
        Public Property Note As String
        Public Property Players As ICollection(Of EventPlayer)
        Public ReadOnly Property Name As String
            Get
                Return String.Join(" vs ", Players.Select(Function(x) x.DisplayName))
            End Get
        End Property

    End Class
End Namespace