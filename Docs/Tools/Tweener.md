# Master Server Toolkit - Tweener

## Описание
Гибкая система для создания анимированных переходов, интерполяций и отложенных действий. Tweener позволяет плавно изменять значения различных типов (float, int, string) с течением времени, а также создавать последовательности действий.

## Основные возможности

### Базовый Tweener
Общий класс для управления и запуска всех Tween-действий.

```csharp
// Запуск простого действия
TweenerActionInfo actionInfo = Tweener.Start(() => {
    // Возвращаем true, когда действие завершено
    return true;
});

// Отмена действия
actionInfo.Cancel();
// или
Tweener.Cancel(actionInfo);
// или по ID
Tweener.Cancel(actionId);

// Проверка, выполняется ли действие
bool isRunning = actionInfo.IsRunning;
// или
bool isRunning = Tweener.IsRunning(actionInfo);
// или по ID
bool isRunning = Tweener.IsRunning(actionId);

// Обработка завершения
actionInfo.OnComplete((id) => {
    Debug.Log($"Действие {id} завершено");
});
// или
Tweener.AddOnCompleteListener(actionInfo, (id) => {
    Debug.Log($"Действие {id} завершено");
});
```

### Tween.Float
Плавная интерполяция значений с плавающей точкой.

```csharp
// Переход от 0 до 1 за 2 секунды с линейной интерполяцией
TweenerActionInfo actionInfo = Tween.Float(0f, 1f, 2f, Easing.Linear, (float value) => {
    // Обновление значения во время анимации
    myCanvasGroup.alpha = value;
});

// Переход с отложенным стартом
TweenerActionInfo actionInfo = Tween.Float(0f, 1f, 2f, Easing.Linear, (float value) => {
    myCanvasGroup.alpha = value;
}, 1f); // Задержка в 1 секунду

// Переход с кастомной кривой анимации
AnimationCurve curve = new AnimationCurve(
    new Keyframe(0, 0),
    new Keyframe(0.5f, 0.8f),
    new Keyframe(1, 1)
);

TweenerActionInfo actionInfo = Tween.Float(0f, 1f, 2f, curve, (float value) => {
    myCanvasGroup.alpha = value;
});

// Переход с обратной анимацией (пинг-понг)
TweenerActionInfo actionInfo = Tween.Float(0f, 1f, 2f, Easing.Linear, (float value) => {
    myCanvasGroup.alpha = value;
}, 0f, true);
```

### Tween.Int
Плавная интерполяция целочисленных значений.

```csharp
// Анимация счетчика от 0 до 100 за 3 секунды
TweenerActionInfo actionInfo = Tween.Int(0, 100, 3f, Easing.OutQuad, (int value) => {
    scoreText.text = value.ToString();
});

// Анимация с обратным отсчетом
TweenerActionInfo actionInfo = Tween.Int(10, 0, 5f, Easing.Linear, (int value) => {
    countdownText.text = value.ToString();
});
```

### Tween.String
Анимированная замена символов в строке.

```csharp
// Анимация печатающегося текста
TweenerActionInfo actionInfo = Tween.String("", "Привет, мир!", 2f, (string value) => {
    dialogText.text = value;
});

// Анимация с маской ввода
TweenerActionInfo actionInfo = Tween.String("", "12345", 1f, (string value) => {
    passwordText.text = new string('*', value.Length);
});
```

### Вспомогательные методы

#### Wait - ожидание заданного времени
```csharp
// Ожидание 2 секунды и выполнение действия
TweenerActionInfo actionInfo = Tween.Wait(2f, () => {
    DoSomething();
});

// Ожидание в последовательности действий
StartCoroutine(WaitExample());

IEnumerator WaitExample()
{
    Debug.Log("Действие 1");
    
    // Ожидание через Tween
    var waitInfo = Tween.Wait(2f, null);
    yield return new WaitUntil(() => !waitInfo.IsRunning);
    
    Debug.Log("Действие 2");
}
```

#### Sequence - последовательность действий
```csharp
// Создание последовательности действий
Tween.Sequence()
    .Add(() => {
        Debug.Log("Шаг 1");
        return true;
    })
    .Wait(1f)
    .Add(() => {
        Debug.Log("Шаг 2");
        return true;
    })
    .Wait(1f)
    .Add(() => {
        Debug.Log("Шаг 3");
        return true;
    })
    .Start();
```

#### RepeatForever - бесконечное повторение
```csharp
// Пульсация объекта
Tween.RepeatForever(() => {
    return Tween.Sequence()
        .Add(Tween.Float(1f, 1.2f, 0.5f, Easing.InOutQuad, (value) => {
            transform.localScale = new Vector3(value, value, value);
        }))
        .Add(Tween.Float(1.2f, 1f, 0.5f, Easing.InOutQuad, (value) => {
            transform.localScale = new Vector3(value, value, value);
        }));
});
```

## Функции анимации (Easing)

Tweener поддерживает различные функции анимации для управления характером перехода:

```csharp
// Линейная интерполяция
Easing.Linear

// Квадратичные функции
Easing.InQuad
Easing.OutQuad
Easing.InOutQuad

// Кубические функции
Easing.InCubic
Easing.OutCubic
Easing.InOutCubic

// Пружинные функции
Easing.InBounce
Easing.OutBounce
Easing.InOutBounce

// Эластичные функции
Easing.InElastic
Easing.OutElastic
Easing.InOutElastic

// Кривая интерполяции из редактора
AnimationCurve customCurve = new AnimationCurve(...);
```

## Примеры использования

### Анимация UI элементов
```csharp
// Плавное появление панели
void ShowPanel()
{
    panel.gameObject.SetActive(true);
    panel.transform.localScale = Vector3.zero;
    
    Tween.Sequence()
        .Add(Tween.Float(0f, 1f, 0.3f, Easing.OutBack, (value) => {
            panel.transform.localScale = new Vector3(value, value, value);
        }))
        .Add(Tween.Float(0f, 1f, 0.2f, Easing.OutQuad, (value) => {
            panel.GetComponent<CanvasGroup>().alpha = value;
        }))
        .Start();
}

// Плавное исчезновение панели
void HidePanel()
{
    Tween.Sequence()
        .Add(Tween.Float(1f, 0f, 0.2f, Easing.InQuad, (value) => {
            panel.GetComponent<CanvasGroup>().alpha = value;
        }))
        .Add(Tween.Float(1f, 0f, 0.3f, Easing.InBack, (value) => {
            panel.transform.localScale = new Vector3(value, value, value);
        }))
        .Add(() => {
            panel.gameObject.SetActive(false);
            return true;
        })
        .Start();
}
```

### Анимация камеры
```csharp
// Плавное перемещение камеры
void MoveCamera(Vector3 targetPosition, float duration)
{
    Vector3 startPosition = Camera.main.transform.position;
    
    Tween.Float(0f, 1f, duration, Easing.InOutQuad, (value) => {
        Camera.main.transform.position = Vector3.Lerp(startPosition, targetPosition, value);
    });
}

// Плавное изменение поля зрения камеры
void ZoomCamera(float targetFOV, float duration)
{
    float startFOV = Camera.main.fieldOfView;
    
    Tween.Float(0f, 1f, duration, Easing.OutQuad, (value) => {
        Camera.main.fieldOfView = Mathf.Lerp(startFOV, targetFOV, value);
    });
}
```

### Создание игровых эффектов
```csharp
// Эффект мигания при уроне
void DamageFlash(SpriteRenderer renderer)
{
    Color normalColor = renderer.color;
    Color flashColor = Color.red;
    
    Tween.Sequence()
        .Add(Tween.Float(0f, 1f, 0.1f, Easing.Linear, (value) => {
            renderer.color = Color.Lerp(normalColor, flashColor, value);
        }))
        .Add(Tween.Float(1f, 0f, 0.1f, Easing.Linear, (value) => {
            renderer.color = Color.Lerp(normalColor, flashColor, value);
        }))
        .Start();
}

// Эффект пульсации
void PulseEffect(Transform target)
{
    Vector3 originalScale = target.localScale;
    Vector3 targetScale = originalScale * 1.2f;
    
    Tween.Sequence()
        .Add(Tween.Float(0f, 1f, 0.3f, Easing.OutQuad, (value) => {
            target.localScale = Vector3.Lerp(originalScale, targetScale, value);
        }))
        .Add(Tween.Float(1f, 0f, 0.3f, Easing.InQuad, (value) => {
            target.localScale = Vector3.Lerp(originalScale, targetScale, value);
        }))
        .Start();
}
```

## Лучшие практики

1. **Отменяйте незавершенные действия** при уничтожении объектов или смене сцен
2. **Группируйте связанные анимации** в последовательности для лучшего контроля
3. **Используйте подходящие функции** анимации для разных типов движения
4. **Хранитие ссылки на TweenerActionInfo** для возможности отмены или проверки статуса
5. **Обрабатывайте завершение** с помощью OnComplete для последовательных операций
6. **Не злоупотребляйте бесконечными циклами** (RepeatForever), не забывайте их отменять
7. **Используйте Wait** вместо WaitForSeconds в корутинах для единообразия
