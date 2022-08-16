using UnityEngine;

namespace VL2D.Common
{
    /// <summary>
    /// 汎用的なメソッドを雑に放り込んでいく場所
    /// </summary>
    public static class CommonUtility
    {
        /// <summary>
        /// 2つのベクトルの内積が指定角度(弧度法)の範囲内であるか判定
        /// </summary>
        /// <param name="vec1">ベクトル1</param>
        /// <param name="vec2">ベクトル2</param>
        /// <param name="radian">指定のラジアン角度</param>
        /// <returns>true: 内積が範囲内である</returns>
        public static bool IsInDotRadian(Vector3 vec1, Vector3 vec2, float radian)
        {
            var differenceRadius = Mathf.Acos(Vector3.Dot(vec1.normalized, vec2.normalized));
            return differenceRadius <= radian;
        }

        /// <summary>
        /// 2つのベクトルの内積が指定角度(度数法)の範囲内であるか判定
        /// </summary>
        /// <param name="vec1">ベクトル1</param>
        /// <param name="vec2">ベクトル2</param>
        /// <param name="radian">指定の角度</param>
        /// <returns>true: 内積が範囲内である</returns>
        public static bool IsInDotDegree(Vector3 vec1, Vector3 vec2, float degree)
        {
            var differenceRadius = Mathf.Acos(Vector3.Dot(vec1.normalized, vec2.normalized));
            var differenceDegree = differenceRadius * Mathf.Rad2Deg;
            return differenceDegree <= degree;
        }
    }
}
