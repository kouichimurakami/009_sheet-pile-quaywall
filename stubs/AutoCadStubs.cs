// AutoCAD / Civil 3D 2025 API のコンパイル専用スタブ。
//
// 実機が無い環境で SheetPileQuayWall.Plugin の構文・型レベル検証を行うためだけに存在する。
// 実装は全て NotSupportedException を投げる。誤って配布・ロードされた場合に
// 沈黙して誤動作するのではなく、必ず即座に失敗させるためである。
//
// ここに定義したメンバは SheetPileQuayWall.Plugin が実際に使用するものだけに限定している。
// 実 API のシグネチャと一致していることは目視でしか保証できない（§9.3 未検証）。
//
// 移植元: 008_tairod@ff3a986 stubs/AutoCadStubs.cs（タイロッド Plugin が使う分だけ）。
// フェーズ 4 で前壁・控え杭のコマンドを実装する際、007/006 由来の API
// （Region / Solid3d.Extrude / BooleanOperation / Editor プロンプト各種）を
// 追記して拡張する。以後この 009 側のファイルが正であり、008 へ戻す必要はない。

namespace Autodesk.AutoCAD.Runtime
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class CommandMethodAttribute : System.Attribute
    {
        public CommandMethodAttribute(string globalName) { GlobalName = globalName; }
        public string GlobalName { get; }
    }
}

namespace Autodesk.AutoCAD.Geometry
{
    public struct Point3d
    {
        public Point3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public static Point3d Origin { get { return new Point3d(0.0, 0.0, 0.0); } }

        public Point3d TransformBy(Matrix3d transform)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }
    }

    public struct Vector3d
    {
        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public static Vector3d YAxis { get { return new Vector3d(0.0, 1.0, 0.0); } }
    }

    public struct Matrix3d
    {
        public static Matrix3d Rotation(double angle, Vector3d axis, Point3d center)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public static Matrix3d Displacement(Vector3d vector)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public static Matrix3d operator *(Matrix3d left, Matrix3d right)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }
    }
}

namespace Autodesk.AutoCAD.Colors
{
    public enum ColorMethod { ByAci }

    public sealed class Color
    {
        public static Color FromColorIndex(ColorMethod method, short index)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public short ColorIndex
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }
}

namespace Autodesk.AutoCAD.DatabaseServices
{
    public enum OpenMode { ForRead, ForWrite }

    public enum DxfCode
    {
        ExtendedDataRegAppName = 1001,
        ExtendedDataAsciiString = 1000
    }

    public struct ObjectId
    {
        public bool IsNull { get { return false; } }
    }

    public struct TypedValue
    {
        public TypedValue(int typeCode, object value) { TypeCode = (short)typeCode; Value = value; }
        public short TypeCode { get; }
        public object Value { get; }
    }

    public sealed class ResultBuffer : System.IDisposable
    {
        public ResultBuffer(params TypedValue[] values) { }
        public TypedValue[] AsArray()
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }
        public void Dispose() { }
    }

    public abstract class DBObject : System.IDisposable
    {
        public void Dispose() { }
    }

    public abstract class Entity : DBObject
    {
        public string Layer
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
            set { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public Autodesk.AutoCAD.Colors.Color Color
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
            set { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public ResultBuffer XData
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
            set { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public void Erase() { throw new System.NotSupportedException("AutoCAD スタブです。"); }

        public void TransformBy(Autodesk.AutoCAD.Geometry.Matrix3d transform)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }
    }

    public sealed class Solid3d : Entity
    {
        public void CreateFrustum(double height, double xRadius, double yRadius, double topRadius)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }
    }

    public sealed class BlockTable : DBObject
    {
        public ObjectId this[string key]
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public sealed class BlockTableRecord : DBObject
    {
        public const string ModelSpace = "*Model_Space";

        public ObjectId AppendEntity(Entity entity)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }
    }

    public sealed class LayerTable : DBObject
    {
        public bool Has(string name) { throw new System.NotSupportedException("AutoCAD スタブです。"); }

        public ObjectId this[string key]
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public ObjectId Add(LayerTableRecord record)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public void UpgradeOpen() { throw new System.NotSupportedException("AutoCAD スタブです。"); }
    }

    public sealed class LayerTableRecord : DBObject
    {
        public string Name
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
            set { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public Autodesk.AutoCAD.Colors.Color Color
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
            set { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public sealed class RegAppTable : DBObject
    {
        public bool Has(string name) { throw new System.NotSupportedException("AutoCAD スタブです。"); }

        public ObjectId Add(RegAppTableRecord record)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public void UpgradeOpen() { throw new System.NotSupportedException("AutoCAD スタブです。"); }
    }

    public sealed class RegAppTableRecord : DBObject
    {
        public string Name
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
            set { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public abstract class Transaction : System.IDisposable
    {
        public DBObject GetObject(ObjectId id, OpenMode mode)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public void AddNewlyCreatedDBObject(DBObject obj, bool add)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public void Commit() { throw new System.NotSupportedException("AutoCAD スタブです。"); }

        public void Dispose() { }
    }

    public abstract class TransactionManager
    {
        public Transaction StartTransaction()
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }
    }

    public sealed class Database
    {
        public TransactionManager TransactionManager
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public ObjectId BlockTableId
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public ObjectId LayerTableId
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public ObjectId RegAppTableId
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }
}

namespace Autodesk.AutoCAD.EditorInput
{
    public enum PromptStatus { OK, None, Cancel, Error, Keyword }

    public sealed class KeywordCollection
    {
        public void Add(string globalName) { }
        public string Default { get; set; }
    }

    public class PromptOptions
    {
        public bool AllowNone { get; set; }
    }

    public sealed class PromptDoubleOptions : PromptOptions
    {
        public PromptDoubleOptions(string message) { Message = message; }
        public string Message { get; }
        public double DefaultValue { get; set; }
        public bool UseDefaultValue { get; set; }
        public bool AllowNegative { get; set; }
        public bool AllowZero { get; set; }
    }

    public sealed class PromptIntegerOptions : PromptOptions
    {
        public PromptIntegerOptions(string message) { Message = message; }
        public string Message { get; }
        public int DefaultValue { get; set; }
        public bool UseDefaultValue { get; set; }
        public int LowerLimit { get; set; }
        public int UpperLimit { get; set; }
    }

    public sealed class PromptKeywordOptions : PromptOptions
    {
        public PromptKeywordOptions(string message) { Message = message; Keywords = new KeywordCollection(); }
        public string Message { get; }
        public KeywordCollection Keywords { get; }
    }

    public sealed class PromptPointOptions : PromptOptions
    {
        public PromptPointOptions(string message) { Message = message; }
        public string Message { get; }
    }

    public sealed class PromptEntityOptions : PromptOptions
    {
        public PromptEntityOptions(string message) { Message = message; }
        public string Message { get; }
        public void SetRejectMessage(string message) { }
        public void AddAllowedClass(System.Type type, bool exactMatch) { }
    }

    public class PromptResult
    {
        public PromptStatus Status
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public string StringResult
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public sealed class PromptDoubleResult : PromptResult
    {
        public double Value
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public sealed class PromptIntegerResult : PromptResult
    {
        public int Value
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public sealed class PromptPointResult : PromptResult
    {
        public Autodesk.AutoCAD.Geometry.Point3d Value
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public sealed class PromptEntityResult : PromptResult
    {
        public Autodesk.AutoCAD.DatabaseServices.ObjectId ObjectId
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public sealed class Editor
    {
        public Autodesk.AutoCAD.Geometry.Matrix3d CurrentUserCoordinateSystem
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public void WriteMessage(string message)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public PromptDoubleResult GetDouble(PromptDoubleOptions options)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public PromptIntegerResult GetInteger(PromptIntegerOptions options)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public PromptResult GetKeywords(PromptKeywordOptions options)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public PromptPointResult GetPoint(PromptPointOptions options)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }

        public PromptEntityResult GetEntity(PromptEntityOptions options)
        {
            throw new System.NotSupportedException("AutoCAD スタブです。");
        }
    }
}

namespace Autodesk.AutoCAD.ApplicationServices
{
    public sealed class Document
    {
        public Autodesk.AutoCAD.DatabaseServices.Database Database
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }

        public Autodesk.AutoCAD.EditorInput.Editor Editor
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public sealed class DocumentCollection
    {
        public Document MdiActiveDocument
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }

    public static class Application
    {
        public static DocumentCollection DocumentManager
        {
            get { throw new System.NotSupportedException("AutoCAD スタブです。"); }
        }
    }
}
