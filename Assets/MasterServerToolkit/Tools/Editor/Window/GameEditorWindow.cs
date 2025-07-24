#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MasterServerToolkit.Editor
{
    /// <summary>
    /// Base class for all game editor windows providing a complete toolkit for creating professional interfaces.
    /// Uses lambda-based approach for all panel types to ensure consistent API and prevent common GUI mistakes.
    /// All content is drawn through lambda functions, eliminating the need to manage Begin/End pairs manually.
    /// </summary>
    public abstract class GameEditorWindow<T> : EditorWindow where T : EditorWindow
    {
        #region Interface Design Constants

        // === COLOR PALETTE ===
        // Unified color scheme for all editor windows to maintain visual consistency
        protected static readonly Color PRIMARY_COLOR = new Color(0.3f, 0.7f, 1f);      // Blue for primary actions
        protected static readonly Color SECONDARY_COLOR = new Color(0.7f, 0.7f, 0.7f);  // Gray for secondary actions
        protected static readonly Color SUCCESS_COLOR = new Color(0.3f, 0.8f, 0.3f);           // Green for positive actions
        protected static readonly Color WARNING_COLOR = new Color(1f, 0.8f, 0.2f);             // Yellow for warnings
        protected static readonly Color ERROR_COLOR = new Color(1f, 0.3f, 0.3f);               // Red for errors and deletion

        // === BACKGROUND AND BORDER COLORS ===
        // Create visual hierarchy through color gradation
        protected static readonly Color HEADER_BACKGROUND = new Color(0.2f, 0.2f, 0.2f);       // Dark background for header
        protected static readonly Color PANEL_BACKGROUND = new Color(0.25f, 0.25f, 0.25f);     // Main panel background
        protected static readonly Color SUBPANEL_BACKGROUND = new Color(0.22f, 0.22f, 0.22f);  // Slightly darker for sub-panels
        protected static readonly Color BORDER_COLOR = new Color(0.1f, 0.1f, 0.1f);            // Border and separator color

        // === SIZES AND SPACING ===
        // Standard dimensions for interface consistency
        protected const float STANDARD_SPACING = 10f;    // Standard spacing between elements
        protected const float SMALL_SPACING = 5f;        // Small spacing for fine grouping
        protected const float LARGE_SPACING = 20f;       // Large spacing for section separation
        protected const float BUTTON_HEIGHT = 30f;       // Standard button height
        protected const float BORDER_WIDTH = 1f;         // Separator line thickness

        #endregion

        #region GUI Style Caching and Scroll State Management

        protected static T Window => GetWindow<T>();

        // Cache styles for performance optimization.
        // Style creation is expensive, so we do it only once per style
        private GUIStyle _mainHeaderStyle;      // Main window header style
        private GUIStyle _sectionHeaderStyle;   // Section header style
        private GUIStyle _panelStyle;           // Main panel style
        private GUIStyle _subPanelStyle;        // Sub-panel style
        private GUIStyle _groupBoxStyle;        // Invisible grouping style

        // Scroll state management for scroll areas
        // Each scroll area needs to maintain its position between frames
        private Vector2 _mainScrollPosition = Vector2.zero;     // Default scroll area position
        private Dictionary<string, Vector2> _namedScrollPositions = new Dictionary<string, Vector2>();  // Named scroll areas

        /// <summary>
        /// Style for main window header.
        /// Large, bold, centered text for tool identification.
        /// </summary>
        protected GUIStyle HeaderStyle
        {
            get
            {
                if (_mainHeaderStyle == null)
                {
                    _mainHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 16,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return _mainHeaderStyle;
            }
        }

        /// <summary>
        /// Style for section headers and sub-panel titles.
        /// Medium size, left-aligned for content structuring.
        /// </summary>
        protected GUIStyle SubheaderStyle
        {
            get
            {
                if (_sectionHeaderStyle == null)
                {
                    _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft
                    };
                }
                return _sectionHeaderStyle;
            }
        }

        /// <summary>
        /// Style for main content panels.
        /// Creates visually separated area with own background.
        /// </summary>
        protected GUIStyle PanelStyle
        {
            get
            {
                if (_panelStyle == null)
                {
                    _panelStyle = new GUIStyle()
                    {
                        normal = { background = CreateColorTexture(PANEL_BACKGROUND) },
                        border = new RectOffset(1, 1, 1, 1),
                        padding = new RectOffset(10, 10, 10, 10)
                    };
                }
                return _panelStyle;
            }
        }

        /// <summary>
        /// Style for sub-panels - nested areas within main panels.
        /// Slightly darker than main panels to create visual hierarchy.
        /// </summary>
        protected GUIStyle SubPanelStyle
        {
            get
            {
                if (_subPanelStyle == null)
                {
                    _subPanelStyle = new GUIStyle()
                    {
                        normal = { background = CreateColorTexture(SUBPANEL_BACKGROUND) },
                        border = new RectOffset(1, 1, 1, 1),
                        padding = new RectOffset(8, 8, 8, 8),    // Smaller padding to save space
                        margin = new RectOffset(5, 5, 5, 5)     // Margin from parent panel
                    };
                }
                return _subPanelStyle;
            }
        }

        /// <summary>
        /// Style for logical grouping of elements without visible borders.
        /// Provides spacing and organization without extra visual elements.
        /// </summary>
        protected GUIStyle GroupBoxStyle
        {
            get
            {
                if (_groupBoxStyle == null)
                {
                    _groupBoxStyle = new GUIStyle()
                    {
                        padding = new RectOffset(10, 10, 5, 5),
                        margin = new RectOffset(0, 0, 5, 5)
                    };
                }
                return _groupBoxStyle;
            }
        }

        #endregion

        #region Window Architecture and Lifecycle

        /// <summary>
        /// Called when window is created or activated.
        /// Override for data initialization or event subscription.
        /// </summary>
        protected virtual void OnEnable()
        {
            EditorApplication.projectChanged += ProjectChanged;
        }

        /// <summary>
        /// Called when window is closed or deactivated.
        /// Override for resource cleanup or event unsubscription.
        /// </summary>
        protected virtual void OnDisable()
        {
            EditorApplication.projectChanged -= ProjectChanged;
        }

        /// <summary>
        /// Main GUI drawing method. Defines the architecture for all windows.
        /// Each child window inherits this structure: header, content, footer.
        /// </summary>
        protected virtual void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            // Draw window header with tool name
            DrawHeader();

            // Main content area - child classes place their functionality here
            EditorGUILayout.BeginVertical(PanelStyle);
            DrawMainContent();
            EditorGUILayout.EndVertical();

            // Footer with action buttons
            DrawFooter();

            EditorGUILayout.EndVertical();

            // Handle user events (keyboard, mouse)
            HandleEvents();
        }

        /// <summary>
        /// Window header drawing.
        /// Creates uniform appearance for all tools.
        /// </summary>
        protected virtual void DrawHeader(bool upperCase = true)
        {
            GUILayout.Space(LARGE_SPACING);
            // Create fixed-height area for header
            EditorGUILayout.BeginHorizontal();
            if (upperCase)
            {
                GUILayout.Label(GetWindowTitle().ToUpper(), HeaderStyle);
            }
            else
            {
                GUILayout.Label(GetWindowTitle(), HeaderStyle);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(LARGE_SPACING);

            // Add separator line under header
            DrawHorizontalLine();
        }

        /// <summary>
        /// Main window content.
        /// Each child window MUST override this method.
        /// </summary>
        protected abstract void DrawMainContent();

        /// <summary>
        /// Window footer with action buttons.
        /// Child classes can override to add specific buttons.
        /// </summary>
        protected virtual void DrawFooter()
        {
            DrawHorizontalLine();

            GUILayout.Space(SMALL_SPACING);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();  // Push buttons to right edge

            if (DrawStyledButton("Close", ERROR_COLOR))
            {
                Close();
            }

            GUILayout.Space(SMALL_SPACING);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// User event handling.
        /// Standard handling: close window on Escape.
        /// </summary>
        protected virtual void HandleEvents()
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            {
                Close();
                currentEvent.Use();  // Mark event as handled
            }
        }

        /// <summary>
        /// Returns window title for header display.
        /// Uses class name by default, but can be overridden.
        /// </summary>
        protected virtual string GetWindowTitle()
        {
            return GetType().Name;
        }

        #endregion

        #region Lambda-Based Interface Structure Methods

        /// <summary>
        /// Creates a scrollable area that can contain any content including sub-panels and other interface elements.
        /// Perfect for complex interfaces that might exceed window height. The scroll position is automatically 
        /// maintained between frames. Use this as the outermost container for scrollable content.
        /// </summary>
        /// <param name="content">Lambda function containing all the scrollable content</param>
        /// <param name="scrollId">Optional unique identifier for this scroll area (allows multiple independent scroll areas)</param>
        /// <param name="alwaysShowHorizontal">Always show horizontal scrollbar</param>
        /// <param name="alwaysShowVertical">Always show vertical scrollbar</param>
        protected void DrawScrollArea(Action content, string scrollId = "main", bool alwaysShowHorizontal = false, bool alwaysShowVertical = false)
        {
            // Get or create scroll position for this scroll area
            Vector2 scrollPosition = GetScrollPosition(scrollId);

            // Determine scroll bar visibility options
            GUIStyle horizontalScrollbar = alwaysShowHorizontal ? GUI.skin.horizontalScrollbar : GUIStyle.none;
            GUIStyle verticalScrollbar = alwaysShowVertical ? GUI.skin.verticalScrollbar : GUIStyle.none;

            // Create the scroll view and capture the new scroll position
            Vector2 newScrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                horizontalScrollbar,
                verticalScrollbar);

            // Update stored scroll position for next frame
            SetScrollPosition(scrollId, newScrollPosition);

            try
            {
                // Execute the content drawing code within the scroll area
                content?.Invoke();
            }
            finally
            {
                // Always end scroll view, even if content throws an exception
                // This ensures GUI state remains consistent
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// Creates a fixed-height scrollable area with specified dimensions.
        /// Useful when you need precise control over scroll area size, such as for embedded lists 
        /// or when you want to ensure the scroll area doesn't take up the entire window.
        /// </summary>
        /// <param name="content">Lambda function containing the scrollable content</param>
        /// <param name="height">Fixed height of the scroll area in pixels</param>
        /// <param name="scrollId">Optional unique identifier for this scroll area</param>
        /// <param name="width">Fixed width of the scroll area in pixels (0 = automatic width)</param>
        protected void DrawFixedScrollArea(Action content, float height, string scrollId = "fixed", float width = 0f)
        {
            Vector2 scrollPosition = GetScrollPosition(scrollId);

            // Create layout options based on provided dimensions
            GUILayoutOption[] layoutOptions;
            if (width > 0f)
            {
                layoutOptions = new GUILayoutOption[] { GUILayout.Height(height), GUILayout.Width(width) };
            }
            else
            {
                layoutOptions = new GUILayoutOption[] { GUILayout.Height(height) };
            }

            Vector2 newScrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, layoutOptions);
            SetScrollPosition(scrollId, newScrollPosition);

            try
            {
                content?.Invoke();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// Creates a scrollable sub-panel - combines the visual styling of sub-panels with scroll capability.
        /// This is perfect for sections that contain dynamic content (like lists) that might grow beyond 
        /// a reasonable height. The panel maintains its visual identity while providing scroll functionality.
        /// </summary>
        /// <param name="title">Sub-panel title (empty string for no title)</param>
        /// <param name="content">Lambda function containing the scrollable panel content</param>
        /// <param name="maxHeight">Maximum height before scrolling kicks in (0 = unlimited)</param>
        /// <param name="scrollId">Optional unique identifier for this scroll area</param>
        /// <param name="addSpacing">Add spacing above panel for visual separation</param>
        protected void DrawScrollableSubPanel(string title, Action content, float maxHeight = 300f, string scrollId = null, bool addSpacing = true)
        {
            // Generate unique scroll ID if not provided
            if (string.IsNullOrEmpty(scrollId))
            {
                scrollId = $"subpanel_{title?.Replace(" ", "_") ?? "unnamed"}";
            }

            // Add spacing for visual section separation
            if (addSpacing)
                GUILayout.Space(STANDARD_SPACING);

            // Draw sub-panel title if provided
            if (!string.IsNullOrEmpty(title))
            {
                GUILayout.Label(title, SubheaderStyle);
                GUILayout.Space(SMALL_SPACING);
            }

            // Create the sub-panel with scrollable content
            EditorGUILayout.BeginVertical(SubPanelStyle);

            if (maxHeight > 0f)
            {
                // Fixed height scroll area within the sub-panel
                DrawFixedScrollArea(content, maxHeight, scrollId);
            }
            else
            {
                // Unlimited height scroll area
                DrawScrollArea(content, scrollId);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a sub-panel with optional title and content.
        /// Sub-panels are visually separated areas with their own background.
        /// Used for grouping logically related functionality.
        /// Think of them as "rooms" within the larger interface "building".
        /// </summary>
        /// <param name="title">Sub-panel title (empty string for no title)</param>
        /// <param name="content">Lambda function containing the drawing code for panel content</param>
        /// <param name="addSpacing">Add spacing above panel for visual separation</param>
        protected void DrawSubPanel(string title, Action content, bool addSpacing = true)
        {
            // Add spacing for visual section separation
            if (addSpacing)
                GUILayout.Space(STANDARD_SPACING);

            // Draw sub-panel title if provided
            if (!string.IsNullOrEmpty(title))
            {
                GUILayout.Label(title, SubheaderStyle);
                GUILayout.Space(SMALL_SPACING);
            }

            // Draw the panel with content
            EditorGUILayout.BeginVertical(SubPanelStyle);
            content?.Invoke();  // Execute the provided content drawing code
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a logical group of elements without visible borders.
        /// Groups provide fine organization within sub-panels.
        /// Think of them as "shelves" within the "room" of a sub-panel.
        /// </summary>
        /// <param name="content">Lambda function containing the drawing code for group content</param>
        protected void DrawGroup(Action content, float width = 0)
        {
            if (width > 0f)
            {
                EditorGUILayout.BeginVertical(GroupBoxStyle, GUILayout.Width(width));
            }
            else
            {
                EditorGUILayout.BeginVertical(GroupBoxStyle, GUILayout.ExpandWidth(true));
            }
            content?.Invoke();  // Execute the provided content drawing code
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws elements in a horizontal row.
        /// Used for placing related controls next to each other.
        /// Saves vertical space and shows logical connection between elements.
        /// Use GUILayout.Space() between elements for proper spacing.
        /// </summary>
        /// <param name="content">Lambda function containing the drawing code for horizontal elements</param>
        protected void DrawHorizontalGroup(Action content)
        {
            EditorGUILayout.BeginHorizontal();
            content?.Invoke();  // Execute the provided content drawing code
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Creates a collapsible section with header.
        /// Advanced interface element for complexity management.
        /// Allows users to hide details that are not currently needed.
        /// </summary>
        /// <param name="title">Section title (always visible)</param>
        /// <param name="isExpanded">Reference to variable storing expanded state</param>
        /// <param name="content">Lambda function containing the drawing code for section content</param>
        protected void DrawFoldoutSection(string title, ref bool isExpanded, Action content)
        {
            GUILayout.Space(SMALL_SPACING);

            // Create clickable area for header with collapse triangle
            Rect foldoutRect = EditorGUILayout.GetControlRect();
            isExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, title, true, SubheaderStyle);

            // If section is expanded, display its content
            if (isExpanded)
            {
                // Increase indent for visual hierarchy
                EditorGUI.indentLevel++;

                // Place section content in sub-panel without title
                DrawSubPanel("", content, false);

                // Restore original indent level
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(SMALL_SPACING);
        }

        /// <summary>
        /// Draws a section header with separator line.
        /// Used for structuring content in main panels.
        /// </summary>
        /// <param name="title">Header text</param>
        protected void DrawSectionHeader(string title)
        {
            GUILayout.Space(STANDARD_SPACING);
            GUILayout.Label(title, SubheaderStyle);
            GUILayout.Space(SMALL_SPACING);
            DrawHorizontalLine();
            GUILayout.Space(SMALL_SPACING);
        }

        /// <summary>
        /// Draws an area with padding on all sides.
        /// Convenient for creating visual "margins" around element groups.
        /// </summary>
        /// <param name="content">Lambda function containing the drawing code for padded content</param>
        /// <param name="padding">Padding size in pixels</param>
        protected void DrawPaddedArea(Action content, float padding = STANDARD_SPACING)
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(padding);                // Top padding
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(padding);                // Left padding
            EditorGUILayout.BeginVertical();

            content?.Invoke();  // Execute the provided content drawing code

            EditorGUILayout.EndVertical();
            GUILayout.Space(padding);                // Right padding
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(padding);                // Bottom padding
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Scroll State Management Helper Methods

        /// <summary>
        /// Retrieves the scroll position for a named scroll area.
        /// This enables multiple independent scroll areas within the same window.
        /// Each scroll area maintains its own position state between frames.
        /// </summary>
        /// <param name="scrollId">Unique identifier for the scroll area</param>
        /// <returns>Current scroll position for the specified area</returns>
        private Vector2 GetScrollPosition(string scrollId)
        {
            if (scrollId == "main")
            {
                return _mainScrollPosition;
            }

            if (_namedScrollPositions.TryGetValue(scrollId, out Vector2 position))
            {
                return position;
            }

            // Return zero position for new scroll areas
            return Vector2.zero;
        }

        /// <summary>
        /// Updates the scroll position for a named scroll area.
        /// Called automatically by scroll area methods to maintain state between frames.
        /// </summary>
        /// <param name="scrollId">Unique identifier for the scroll area</param>
        /// <param name="position">New scroll position to store</param>
        private void SetScrollPosition(string scrollId, Vector2 position)
        {
            if (scrollId == "main")
            {
                _mainScrollPosition = position;
            }
            else
            {
                _namedScrollPositions[scrollId] = position;
            }
        }

        /// <summary>
        /// Resets all scroll positions to zero. Useful for window reset functionality.
        /// Call this when you want to return all scroll areas to their initial state.
        /// </summary>
        protected void ResetAllScrollPositions()
        {
            _mainScrollPosition = Vector2.zero;
            _namedScrollPositions.Clear();
        }

        /// <summary>
        /// Resets a specific scroll area to the top. Useful for dynamic content updates.
        /// For example, when loading new data that changes the content height.
        /// </summary>
        /// <param name="scrollId">Identifier of the scroll area to reset</param>
        protected virtual void ResetScrollPosition(string scrollId)
        {
            SetScrollPosition(scrollId, Vector2.zero);
        }

        #endregion

        #region Drawing Helper Methods

        /// <summary>
        /// Draws a horizontal separator line.
        /// Used for visual separation of interface sections.
        /// </summary>
        protected void DrawHorizontalLine()
        {
            Rect lineRect = EditorGUILayout.GetControlRect(false, BORDER_WIDTH);
            EditorGUI.DrawRect(lineRect, BORDER_COLOR);
        }

        /// <summary>
        /// Draws a vertical separator line of specified height.
        /// Useful for separating elements in horizontal groups.
        /// </summary>
        /// <param name="height">Line height in pixels</param>
        protected void DrawVerticalLine(float height)
        {
            Rect lineRect = GUILayoutUtility.GetRect(BORDER_WIDTH, height);
            EditorGUI.DrawRect(lineRect, BORDER_COLOR);
        }

        /// <summary>
        /// Draws a stylized button with colored background.
        /// Used for creating buttons with semantic meaning (success, warning, error).
        /// </summary>
        /// <param name="text">Button text</param>
        /// <param name="color">Button background color</param>
        /// <param name="width">Button width (0 = automatic)</param>
        /// <returns>true if button was clicked</returns>
        protected bool DrawStyledButton(string text, Color color, float width = 0f)
        {
            // Save original background color
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;

            bool wasClicked;
            if (width > 0f)
            {
                wasClicked = GUILayout.Button(text, GUILayout.Width(width), GUILayout.Height(BUTTON_HEIGHT));
            }
            else
            {
                wasClicked = GUILayout.Button(text, GUILayout.Height(BUTTON_HEIGHT));
            }

            // Restore original background color
            GUI.backgroundColor = originalColor;
            return wasClicked;
        }

        /// <summary>
        /// Creates a texture of specified color for use in GUI styles.
        /// Internal method for creating colored backgrounds for buttons and panels.
        /// </summary>
        /// <param name="color">Texture color</param>
        /// <returns>Single-color texture of 1x1 pixel</returns>
        protected Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite"></param>
        protected void DrawSpritePreview(Sprite sprite)
        {
            DrawSpritePreview(128, 128, sprite, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="useSpriteShape"></param>
        protected void DrawSpritePreview(Sprite sprite, bool useSpriteShape)
        {
            DrawSpritePreview(128, 128, sprite, useSpriteShape);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="sprite"></param>
        /// <param name="useSpriteShape"></param>
        protected void DrawSpritePreview(float width, float height, Sprite sprite, bool useSpriteShape)
        {
            DrawSpritePreview(width, height, sprite, useSpriteShape, false, null, new Color(0, 0, 0, 0.3f));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="sprite"></param>
        /// <param name="useSpriteShape"></param>
        /// <param name="allowUpscaling"></param>
        /// <param name="backgroundColor"></param>
        /// <param name="borderColor"></param>
        protected void DrawSpritePreview(
            float width,
            float height,
            Sprite sprite,
            bool useSpriteShape = true,
            bool allowUpscaling = false,
            Color? backgroundColor = null,
            Color? borderColor = null)
        {
            // Автоматически создаем область нужного размера
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

            if (sprite == null)
            {
                DrawEmptyPreview(rect, backgroundColor, borderColor);
                return;
            }

            // Рисуем фон если указан
            if (backgroundColor.HasValue)
            {
                EditorGUI.DrawRect(rect, backgroundColor.Value);
            }

            // Получаем реальные размеры спрайта в пикселях
            Vector2 spriteSize = new Vector2(sprite.textureRect.width, sprite.textureRect.height);

            // Вычисляем область для отрисовки спрайта
            Rect spriteDrawRect = useSpriteShape
                ? CalculateProportionalRect(rect, spriteSize, allowUpscaling)
                : rect;

            // Отрисовываем спрайт с правильными UV координатами
            DrawSpriteWithUVCoords(spriteDrawRect, sprite);

            // Рисуем рамку если указана
            if (borderColor.HasValue)
            {
                DrawBorder(rect, borderColor.Value);
            }
        }

        protected void DrawEmptyPreview(Rect rect, Color? backgroundColor, Color? borderColor)
        {
            // Фон
            Color bgColor = backgroundColor ?? new Color(0.5f, 0.5f, 0.5f, 0.3f);
            EditorGUI.DrawRect(rect, bgColor);

            // Текст placeholder
            GUI.Label(rect, "No Sprite", EditorStyles.centeredGreyMiniLabel);

            // Рамка
            if (borderColor.HasValue)
            {
                DrawBorder(rect, borderColor.Value);
            }
        }

        protected void DrawSpriteWithUVCoords(Rect drawRect, Sprite sprite)
        {
            if (sprite?.texture == null) return;

            Rect spriteRect = sprite.textureRect;
            Rect uvRect = new Rect(
                spriteRect.x / sprite.texture.width,
                spriteRect.y / sprite.texture.height,
                spriteRect.width / sprite.texture.width,
                spriteRect.height / sprite.texture.height
            );

            GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, uvRect);
        }

        protected void DrawBorder(Rect rect, Color borderColor)
        {
            // Рисуем рамку по периметру
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), borderColor); // Верх
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), borderColor); // Низ
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), borderColor); // Лево
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), borderColor); // Право
        }

        #endregion

        #region Window Messages

        protected virtual void DrawInfoMessage(string text)
        {
            EditorGUILayout.HelpBox(text, MessageType.Info);
        }

        protected virtual void DrawWarnMessage(string text)
        {
            EditorGUILayout.HelpBox(text, MessageType.Warning);
        }

        protected virtual void DrawErrorMessage(string text)
        {
            EditorGUILayout.HelpBox(text, MessageType.Error);
        }

        protected virtual void DrawNeutralMessage(string text)
        {
            EditorGUILayout.HelpBox(text, MessageType.None);
        }

        #endregion

        #region Window Lifecycle Methods

        /// <summary>
        /// Sets title and tooltip of current window
        /// </summary>
        /// <param name="text"></param>
        /// <param name="tooltip"></param>
        protected virtual void Title(string text, string tooltip)
        {
            Window.titleContent = new GUIContent(text, tooltip);
        }

        /// <summary>
        /// Sets size of current window
        /// </summary>
        /// <param name="minW"></param>
        /// <param name="minH"></param>
        /// <param name="maxW"></param>
        /// <param name="maxH"></param>
        protected void Size(float minW, float minH, float maxW, float maxH)
        {
            Window.minSize = new Vector2(minW, minH);
            Window.maxSize = new Vector2(maxW, maxH);
        }

        #endregion

        #region EVENT HANDLERS

        protected virtual void ProjectChanged() { }

        #endregion

        #region UTILS

        protected Vector2 CalculateDisplaySize(Sprite sprite, Vector2 maxSize, bool useSpriteShape, bool allowUpscaling)
        {
            if (sprite == null) return maxSize;

            if (!useSpriteShape) return maxSize;

            Vector2 spriteSize = new Vector2(sprite.textureRect.width, sprite.textureRect.height);
            Rect tempRect = new Rect(0, 0, maxSize.x, maxSize.y);
            Rect finalRect = CalculateProportionalRect(tempRect, spriteSize, allowUpscaling);

            return new Vector2(finalRect.width, finalRect.height);
        }

        protected Rect CalculateProportionalRect(Rect targetRect, Vector2 spriteSize, bool allowUpscaling)
        {
            if (spriteSize.x <= 0 || spriteSize.y <= 0)
                return targetRect;

            // Вычисляем коэффициенты масштабирования для каждой оси
            float scaleX = targetRect.width / spriteSize.x;
            float scaleY = targetRect.height / spriteSize.y;

            // Берем минимальный коэффициент, чтобы спрайт полностью поместился
            float scale = Mathf.Min(scaleX, scaleY);

            // Если не разрешено увеличение, ограничиваем масштаб единицей
            if (!allowUpscaling)
            {
                scale = Mathf.Min(scale, 1.0f);
            }

            // Вычисляем итоговые размеры спрайта
            float finalWidth = spriteSize.x * scale;
            float finalHeight = spriteSize.y * scale;

            // Центрируем спрайт в целевой области
            float offsetX = (targetRect.width - finalWidth) * 0.5f;
            float offsetY = (targetRect.height - finalHeight) * 0.5f;

            return new Rect(
                targetRect.x + offsetX,
                targetRect.y + offsetY,
                finalWidth,
                finalHeight
            );
        }

        #endregion
    }
}
#endif