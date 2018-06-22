Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports Newtonsoft.Json

Namespace Data.Schedule

    Public Class ScheduleEvent
        <JsonProperty("when")>
        Public WhenDate As DateTime
        Public Id As Integer
        Public Match1 As EventMatch

        Public Channels As ICollection(Of EventChannel)

    End Class
End Namespace


