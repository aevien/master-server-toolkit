using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class DataTableLayoutGroup : LayoutGroup
    {
        #region INSPECTOR

        [SerializeField]
        private DataTableColInfo[] collsInfo;
        [SerializeField]
        private float cellSpacing = 0;
        [SerializeField]
        private float rowSpacing = 0;
        [SerializeField]
        private float minRowHeight = 30;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<RectTransform> Children => rectChildren;

        /// <summary>
        /// 
        /// </summary>
        public RectTransform RectTransform => rectTransform;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int TotalRows()
        {
            int totalCols = TotalCols();
            int totalRows = 0;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                if (i % totalCols == 0)
                {
                    totalRows++;
                }
            }

            return totalRows;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int TotalCols()
        {
            return collsInfo != null && collsInfo.Length >= 1 ? collsInfo.Length : 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="colIndex"></param>
        /// <returns></returns>
        private float CalculatedColWidth(int colIndex)
        {
            // The total number of cols
            int totalCols = TotalCols();
            // Parent rect width
            float rectWidth = rectTransform.rect.size.x;

            // if we have one col
            if (totalCols == 1)
            {
                return rectTransform.rect.size.x - padding.horizontal;
            }

            // If we have more than one col
            if (totalCols > 1 && colIndex - 1 <= collsInfo.Length && collsInfo[colIndex].width > 0)
            {
                return collsInfo[colIndex].width;
            }

            // If we have more than one col but width of is not defined
            if (totalCols > 1 && colIndex - 1 <= collsInfo.Length && collsInfo[colIndex].width <= 0)
            {
                int definedChildren = collsInfo.Where(ch => ch.width > 0).Count();
                int undefinedChildren = collsInfo.Where(ch => ch.width <= 0).Count();

                float definedWidth = 0f;

                for (int i = 0; i < totalCols; i++)
                {
                    if (collsInfo[i].width > 0)
                    {
                        definedWidth += collsInfo[i].width;
                    }
                }

                return (rectWidth - padding.horizontal - (cellSpacing * undefinedChildren) - (cellSpacing * (definedChildren - 1)) - definedWidth) / undefinedChildren;
            }

            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cols"></param>
        public void SetCols(IEnumerable<DataTableColInfo> cols)
        {
            collsInfo = cols.ToArray();
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
        }

        public override void CalculateLayoutInputVertical()
        {
            int totalRows = TotalRows();
            float minSpace = padding.vertical + (rowSpacing * (totalRows - 1)) + (minRowHeight * totalRows);
            SetLayoutInputForAxis(minSpace, minSpace, -1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            SetCellsAlongAxis(0);
        }

        public override void SetLayoutVertical()
        {
            SetCellsAlongAxis(1);
        }

        private void SetCellsAlongAxis(int axis)
        {
            int rectChildrenCount = rectChildren.Count;
            int currentCellIndex = 0;
            int totalCols = TotalCols();
            int totalRows = TotalRows();

            for (int row = 0; row <= totalRows; row++)
            {
                float xPos = 0;
                float yPos = 0;

                for (int col = 0; col < totalCols; col++)
                {
                    if (currentCellIndex == rectChildrenCount) break;

                    // Get current child
                    RectTransform childRect = rectChildren[currentCellIndex];

                    // Do horizontal logic
                    if (axis == 0)
                    {
                        m_Tracker.Add(this, childRect,
                            DrivenTransformProperties.Anchors |
                            DrivenTransformProperties.AnchoredPosition |
                            DrivenTransformProperties.Pivot |
                            DrivenTransformProperties.SizeDelta);

                        childRect.anchorMin = Vector2.up;
                        childRect.anchorMax = Vector2.up;

                        // If we have only one col
                        if (totalCols <= 1)
                        {
                            childRect.sizeDelta = new Vector2(rectTransform.rect.size.x - padding.horizontal, minRowHeight);
                        }
                        // If we have more than one col
                        else
                        {
                            float cellWidth = CalculatedColWidth(col);
                            childRect.sizeDelta = new Vector2(cellWidth, minRowHeight);
                        }

                        if (col == 0)
                        {
                            xPos += padding.left;
                        }

                        SetChildAlongAxis(rectChildren[currentCellIndex], axis, xPos);

                        xPos += childRect.sizeDelta.x + cellSpacing;
                    }

                    // Do vertical logic
                    if (axis == 1)
                    {
                        yPos = padding.top + (minRowHeight + rowSpacing) * row;

                        SetChildAlongAxis(rectChildren[currentCellIndex], axis, yPos);
                    }

                    currentCellIndex++;
                }
            }
        }
    }

    [Serializable]
    public struct DataTableColInfo
    {
        public float width;
    }
}
