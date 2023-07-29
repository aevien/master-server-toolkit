using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromMatrix4x4(this Matrix4x4 matrix)
        {
            var jsonObject = MstJson.EmptyObject;
            if (matrix.m00 != 0) jsonObject.AddField("m00", matrix.m00);
            if (matrix.m01 != 0) jsonObject.AddField("m01", matrix.m01);
            if (matrix.m02 != 0) jsonObject.AddField("m02", matrix.m02);
            if (matrix.m03 != 0) jsonObject.AddField("m03", matrix.m03);
            if (matrix.m10 != 0) jsonObject.AddField("m10", matrix.m10);
            if (matrix.m11 != 0) jsonObject.AddField("m11", matrix.m11);
            if (matrix.m12 != 0) jsonObject.AddField("m12", matrix.m12);
            if (matrix.m13 != 0) jsonObject.AddField("m13", matrix.m13);
            if (matrix.m20 != 0) jsonObject.AddField("m20", matrix.m20);
            if (matrix.m21 != 0) jsonObject.AddField("m21", matrix.m21);
            if (matrix.m22 != 0) jsonObject.AddField("m22", matrix.m22);
            if (matrix.m23 != 0) jsonObject.AddField("m23", matrix.m23);
            if (matrix.m30 != 0) jsonObject.AddField("m30", matrix.m30);
            if (matrix.m31 != 0) jsonObject.AddField("m31", matrix.m31);
            if (matrix.m32 != 0) jsonObject.AddField("m32", matrix.m32);
            if (matrix.m33 != 0) jsonObject.AddField("m33", matrix.m33);
            return jsonObject;
        }

        // ReSharper disable once InconsistentNaming
        public static Matrix4x4 ToMatrix4x4(this MstJson jsonObject)
        {
            var matrix = new Matrix4x4();
            if (jsonObject["m00"]) matrix.m00 = jsonObject["m00"].FloatValue;
            if (jsonObject["m01"]) matrix.m01 = jsonObject["m01"].FloatValue;
            if (jsonObject["m02"]) matrix.m02 = jsonObject["m02"].FloatValue;
            if (jsonObject["m03"]) matrix.m03 = jsonObject["m03"].FloatValue;
            if (jsonObject["m10"]) matrix.m10 = jsonObject["m10"].FloatValue;
            if (jsonObject["m11"]) matrix.m11 = jsonObject["m11"].FloatValue;
            if (jsonObject["m12"]) matrix.m12 = jsonObject["m12"].FloatValue;
            if (jsonObject["m13"]) matrix.m13 = jsonObject["m13"].FloatValue;
            if (jsonObject["m20"]) matrix.m20 = jsonObject["m20"].FloatValue;
            if (jsonObject["m21"]) matrix.m21 = jsonObject["m21"].FloatValue;
            if (jsonObject["m22"]) matrix.m22 = jsonObject["m22"].FloatValue;
            if (jsonObject["m23"]) matrix.m23 = jsonObject["m23"].FloatValue;
            if (jsonObject["m30"]) matrix.m30 = jsonObject["m30"].FloatValue;
            if (jsonObject["m31"]) matrix.m31 = jsonObject["m31"].FloatValue;
            if (jsonObject["m32"]) matrix.m32 = jsonObject["m32"].FloatValue;
            if (jsonObject["m33"]) matrix.m33 = jsonObject["m33"].FloatValue;
            return matrix;
        }

        public static MstJson ToJson(this Matrix4x4 matrix)
        {
            return matrix.FromMatrix4x4();
        }
    }
}