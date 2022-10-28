/*
Copyright (c) 2010-2021 Matt Schoen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using UnityEngine;

namespace MasterServerToolkit.Json
{
    // ReSharper disable once PartialTypeWithSinglePart
    public static partial class MstJsonTemplates
    {
        /// <summary>
        /// Parse vector 2 from json
        /// </summary>
        /// <param name="jsonObject"></param>
        /// <returns></returns>
        public static Vector2 ToVector2(this MstJson jsonObject)
        {
            var x = jsonObject["x"] ? jsonObject["x"].floatValue : 0;
            var y = jsonObject["y"] ? jsonObject["y"].floatValue : 0;
            return new Vector2(x, y);
        }

        /// <summary>
        /// Create json from vector 2
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static MstJson FromVector2(this Vector2 vector)
        {
            var jsonObject = MstJson.EmptyObject;
            if (vector.x != 0) jsonObject.AddField("x", vector.x);
            if (vector.y != 0) jsonObject.AddField("y", vector.y);
            return jsonObject;
        }

        public static MstJson ToJson(this Vector2 vector)
        {
            return vector.FromVector2();
        }

        /*
		 * Vector3
		 */
        public static MstJson FromVector3(this Vector3 vector)
        {
            var jsonObject = MstJson.EmptyObject;
            if (vector.x != 0) jsonObject.AddField("x", vector.x);
            if (vector.y != 0) jsonObject.AddField("y", vector.y);
            if (vector.z != 0) jsonObject.AddField("z", vector.z);
            return jsonObject;
        }

        public static Vector3 ToVector3(this MstJson jsonObject)
        {
            var x = jsonObject["x"] ? jsonObject["x"].floatValue : 0;
            var y = jsonObject["y"] ? jsonObject["y"].floatValue : 0;
            var z = jsonObject["z"] ? jsonObject["z"].floatValue : 0;
            return new Vector3(x, y, z);
        }

        public static MstJson ToJson(this Vector3 vector)
        {
            return vector.FromVector3();
        }

        /*
		 * Vector4
		 */
        public static MstJson FromVector4(this Vector4 vector)
        {
            var jsonObject = MstJson.EmptyObject;
            if (vector.x != 0) jsonObject.AddField("x", vector.x);
            if (vector.y != 0) jsonObject.AddField("y", vector.y);
            if (vector.z != 0) jsonObject.AddField("z", vector.z);
            if (vector.w != 0) jsonObject.AddField("w", vector.w);
            return jsonObject;
        }

        public static Vector4 ToVector4(this MstJson jsonObject)
        {
            var x = jsonObject["x"] ? jsonObject["x"].floatValue : 0;
            var y = jsonObject["y"] ? jsonObject["y"].floatValue : 0;
            var z = jsonObject["z"] ? jsonObject["z"].floatValue : 0;
            var w = jsonObject["w"] ? jsonObject["w"].floatValue : 0;
            return new Vector4(x, y, z, w);
        }

        public static MstJson ToJson(this Vector4 vector)
        {
            return vector.FromVector4();
        }

        /*
		 * Matrix4x4
		 */
        // ReSharper disable once InconsistentNaming
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
            if (jsonObject["m00"]) matrix.m00 = jsonObject["m00"].floatValue;
            if (jsonObject["m01"]) matrix.m01 = jsonObject["m01"].floatValue;
            if (jsonObject["m02"]) matrix.m02 = jsonObject["m02"].floatValue;
            if (jsonObject["m03"]) matrix.m03 = jsonObject["m03"].floatValue;
            if (jsonObject["m10"]) matrix.m10 = jsonObject["m10"].floatValue;
            if (jsonObject["m11"]) matrix.m11 = jsonObject["m11"].floatValue;
            if (jsonObject["m12"]) matrix.m12 = jsonObject["m12"].floatValue;
            if (jsonObject["m13"]) matrix.m13 = jsonObject["m13"].floatValue;
            if (jsonObject["m20"]) matrix.m20 = jsonObject["m20"].floatValue;
            if (jsonObject["m21"]) matrix.m21 = jsonObject["m21"].floatValue;
            if (jsonObject["m22"]) matrix.m22 = jsonObject["m22"].floatValue;
            if (jsonObject["m23"]) matrix.m23 = jsonObject["m23"].floatValue;
            if (jsonObject["m30"]) matrix.m30 = jsonObject["m30"].floatValue;
            if (jsonObject["m31"]) matrix.m31 = jsonObject["m31"].floatValue;
            if (jsonObject["m32"]) matrix.m32 = jsonObject["m32"].floatValue;
            if (jsonObject["m33"]) matrix.m33 = jsonObject["m33"].floatValue;
            return matrix;
        }

        public static MstJson ToJson(this Matrix4x4 matrix)
        {
            return matrix.FromMatrix4x4();
        }

        /*
		 * Quaternion
		 */
        public static MstJson FromQuaternion(this Quaternion quaternion)
        {
            var jsonObject = MstJson.EmptyObject;
            if (quaternion.w != 0) jsonObject.AddField("w", quaternion.w);
            if (quaternion.x != 0) jsonObject.AddField("x", quaternion.x);
            if (quaternion.y != 0) jsonObject.AddField("y", quaternion.y);
            if (quaternion.z != 0) jsonObject.AddField("z", quaternion.z);
            return jsonObject;
        }

        public static Quaternion ToQuaternion(this MstJson jsonObject)
        {
            var x = jsonObject["x"] ? jsonObject["x"].floatValue : 0;
            var y = jsonObject["y"] ? jsonObject["y"].floatValue : 0;
            var z = jsonObject["z"] ? jsonObject["z"].floatValue : 0;
            var w = jsonObject["w"] ? jsonObject["w"].floatValue : 0;
            return new Quaternion(x, y, z, w);
        }

        public static MstJson ToJson(this Quaternion quaternion)
        {
            return quaternion.FromQuaternion();
        }

        /*
		 * Color
		 */
        public static MstJson FromColor(this Color color)
        {
            var jsonObject = MstJson.EmptyObject;
            if (color.r != 0) jsonObject.AddField("r", color.r);
            if (color.g != 0) jsonObject.AddField("g", color.g);
            if (color.b != 0) jsonObject.AddField("b", color.b);
            if (color.a != 0) jsonObject.AddField("a", color.a);
            return jsonObject;
        }

        public static Color ToColor(this MstJson jsonObject)
        {
            var color = new Color();
            for (var i = 0; i < jsonObject.count; i++)
            {
                switch (jsonObject.keys[i])
                {
                    case "r":
                        color.r = jsonObject[i].floatValue;
                        break;
                    case "g":
                        color.g = jsonObject[i].floatValue;
                        break;
                    case "b":
                        color.b = jsonObject[i].floatValue;
                        break;
                    case "a":
                        color.a = jsonObject[i].floatValue;
                        break;
                }
            }

            return color;
        }

        public static MstJson ToJson(this Color color)
        {
            return color.FromColor();
        }

        /*
		 * Layer Mask
		 */
        public static MstJson FromLayerMask(this LayerMask layerMask)
        {
            var jsonObject = MstJson.EmptyObject;
            jsonObject.AddField("value", layerMask.value);
            return jsonObject;
        }

        public static LayerMask ToLayerMask(this MstJson jsonObject)
        {
            var layerMask = new LayerMask { value = jsonObject["value"].intValue };
            return layerMask;
        }

        public static MstJson ToJson(this LayerMask layerMask)
        {
            return layerMask.FromLayerMask();
        }

        /*
		 * Rect
		 */
        public static MstJson FromRect(this Rect rect)
        {
            var jsonObject = MstJson.EmptyObject;
            if (rect.x != 0) jsonObject.AddField("x", rect.x);
            if (rect.y != 0) jsonObject.AddField("y", rect.y);
            if (rect.height != 0) jsonObject.AddField("height", rect.height);
            if (rect.width != 0) jsonObject.AddField("width", rect.width);
            return jsonObject;
        }

        public static Rect ToRect(this MstJson jsonObject)
        {
            var rect = new Rect();
            for (var i = 0; i < jsonObject.count; i++)
            {
                switch (jsonObject.keys[i])
                {
                    case "x":
                        rect.x = jsonObject[i].floatValue;
                        break;
                    case "y":
                        rect.y = jsonObject[i].floatValue;
                        break;
                    case "height":
                        rect.height = jsonObject[i].floatValue;
                        break;
                    case "width":
                        rect.width = jsonObject[i].floatValue;
                        break;
                }
            }

            return rect;
        }

        public static MstJson ToJson(this Rect rect)
        {
            return rect.FromRect();
        }

        /*
		* Rect Offset
		 */
        public static MstJson FromRectOffset(this RectOffset rectOffset)
        {
            var jsonObject = MstJson.EmptyObject;
            if (rectOffset.bottom != 0) jsonObject.AddField("bottom", rectOffset.bottom);
            if (rectOffset.left != 0) jsonObject.AddField("left", rectOffset.left);
            if (rectOffset.right != 0) jsonObject.AddField("right", rectOffset.right);
            if (rectOffset.top != 0) jsonObject.AddField("top", rectOffset.top);
            return jsonObject;
        }

        public static RectOffset ToRectOffset(this MstJson jsonObject)
        {
            var rectOffset = new RectOffset();
            for (var i = 0; i < jsonObject.count; i++)
            {
                switch (jsonObject.keys[i])
                {
                    case "bottom":
                        rectOffset.bottom = jsonObject[i].intValue;
                        break;
                    case "left":
                        rectOffset.left = jsonObject[i].intValue;
                        break;
                    case "right":
                        rectOffset.right = jsonObject[i].intValue;
                        break;
                    case "top":
                        rectOffset.top = jsonObject[i].intValue;
                        break;
                }
            }

            return rectOffset;
        }

        public static MstJson ToJson(this RectOffset rectOffset)
        {
            return rectOffset.FromRectOffset();
        }
    }
}
