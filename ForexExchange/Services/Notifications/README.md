# Notification Center System

## Overview
The Notification Center is a centralized system for managing and sending notifications across multiple channels in the ForexExchange application. It supports real-time notifications (SignalR), push notifications, SMS, email, and Telegram notifications through a provider-based architecture.

**Key Features:**
- ✅ **User Exclusion**: Prevents users from seeing popup notifications for their own actions
- ✅ **Multi-Channel Support**: SignalR (real-time), Push notifications, SMS, Email, Telegram
- ✅ **Centralized Management**: Single hub coordinates all notification providers
- ✅ **Provider-Based Architecture**: Easy to add new notification channels

## Architecture

### Core Components

#### 1. INotificationProvider Interface
Base interface for all notification providers with methods for different event types:
- `SendOrderNotificationAsync()` - Order-related notifications
- `SendAccountingDocumentNotificationAsync()` - Document notifications
- `SendCustomerNotificationAsync()` - Customer-related notifications
- `SendSystemNotificationAsync()` - System-wide notifications
- `SendManualAdjustmentNotificationAsync()` - Custom notifications (used for manual adjustments)

#### 2. NotificationHub (Central Coordinator)
Main orchestrator that manages all notification providers and handles:
- Provider registration and management
- Event-specific notification building
- Coordinated sending across multiple channels
- User exclusion logic for preventing self-notifications

#### 3. NotificationContext
Data container that holds all notification information:
- Event type and priority
- Target users and exclusions
- Title, message, and navigation URLs
- Related entity information
- Custom data payload

## Provider Types

### Currently Implemented:
1. **SignalRNotificationProvider** - Real-time browser notifications with user exclusion
2. **PushNotificationProvider** - Web push notifications using VAPID (sends to all admins)
3. **SmsNotificationProvider** - SMS notifications (template)
4. **EmailNotificationProvider** - Email notifications (template)
5. **TelegramNotificationProvider** - Telegram bot notifications (template)

### Provider States:
- **Active**: SignalR and Push providers are fully implemented
- **Template**: SMS, Email, and Telegram providers are scaffolded for future implementation

## Event Types

### Supported Events:
```csharp
public enum NotificationEventType
{
    // Order events
    OrderCreated,
    OrderUpdated,
    OrderCompleted,
    OrderCancelled,

    // Accounting document events
    AccountingDocumentCreated,
    AccountingDocumentVerified,
    AccountingDocumentRejected,

    // Customer events
    CustomerRegistered,
    CustomerBalanceChanged,
    CustomerStatusChanged,

    // System events
    SystemError,
    SystemMaintenance,
    ExchangeRateUpdated,

    // Custom events (used for manual adjustments)
    Custom
}
```

### Priority Levels:
```csharp
public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}
```

## Usage Examples

### 1. Order Notifications
```csharp
// In OrdersController.cs
await _notificationHub.SendOrderNotificationAsync(
    order,
    NotificationEventType.OrderCreated,
    currentUser.Id  // Excludes current user from SignalR popups
);
```

### 2. Manual Transaction Notifications (Custom)
```csharp
// In DatabaseController.cs - Manual balance adjustments
await _notificationHub.SendManualAdjustmentNotificationAsync(
    title: "تعدیل دستی موجودی ایجاد شد",
    message: $"مشتری: {customerName} | مبلغ: {amount:N2} {currencyCode}",
    eventType: NotificationEventType.CustomerBalanceChanged,
    userId: currentUser?.Id, // Excludes current user from popups
    navigationUrl: $"/Reports/CustomerReports?customerId={customerId}",
    priority: NotificationPriority.Normal
);
```

### 3. Accounting Document Notifications
```csharp
// In AccountingDocumentsController.cs
await _notificationHub.SendAccountingDocumentNotificationAsync(
    accountingDocument,
    NotificationEventType.AccountingDocumentCreated,
    currentUser.Id  // Excludes current user from popups
);
```

### 4. System Notifications
```csharp
await _notificationHub.SendManualAdjustmentNotificationAsync(
    title: "System Maintenance",
    message: "Scheduled maintenance will begin in 30 minutes",
    eventType: NotificationEventType.SystemMaintenance,
    priority: NotificationPriority.High
);
```

## User Exclusion Logic (Critical Feature)

### Preventing Self-Notifications:
The system prevents users from seeing popup notifications for actions they performed, while still allowing push notifications:

#### Server-Side Setup:
1. **Controller gets current user**:
   ```csharp
   var currentUser = await _userManager.GetUserAsync(User);
   ```

2. **Pass user ID to notification system**:
   ```csharp
   userId: currentUser?.Id  // This user will be excluded from SignalR popups
   ```

3. **Notification context includes exclusion**:
   ```csharp
   SendToAllAdmins = true, // Send to all admins
   ExcludeUserIds = !string.IsNullOrEmpty(userId) ? new List<string> { userId } : new List<string>()
   ```

#### Client-Side Filtering:
The JavaScript checks for user exclusion before showing notifications:
```javascript
shouldExcludeCurrentUser(notification) {
    if (!window.currentUserId) return false;

    if (notification.data && notification.data.excludeUserIds) {
        const excludeUserIds = Array.isArray(notification.data.excludeUserIds)
            ? notification.data.excludeUserIds
            : [notification.data.excludeUserIds];

        return excludeUserIds.includes(window.currentUserId);
    }
    return false;
}
```

### Behavior Matrix:
| Notification Type | SignalR Popups | Push Notifications | Other Channels |
|-------------------|----------------|-------------------|----------------|
| **Order Events** | ✅ All admins except performer | ✅ All admins | ✅ As configured |
| **Manual Adjustments** | ✅ All admins except performer | ✅ All admins | ✅ As configured |
| **Accounting Documents** | ✅ All admins except performer | ✅ All admins | ✅ As configured |
| **Customer Events** | ✅ All admins except performer | ✅ All admins | ✅ As configured |
| **System Events** | ✅ All admins | ✅ All admins | ✅ As configured |

## Configuration

### 1. Service Registration (Program.cs)
```csharp
// Individual providers
builder.Services.AddScoped<SignalRNotificationProvider>();
builder.Services.AddScoped<PushNotificationProvider>();
builder.Services.AddScoped<SmsNotificationProvider>();
builder.Services.AddScoped<EmailNotificationProvider>();
builder.Services.AddScoped<TelegramNotificationProvider>();

// Central hub with provider registration
builder.Services.AddScoped<INotificationHub>(serviceProvider =>
{
    var hub = new NotificationHub(context, logger, configuration);

    // Register all providers
    hub.RegisterProvider(serviceProvider.GetRequiredService<SignalRNotificationProvider>());
    hub.RegisterProvider(serviceProvider.GetRequiredService<PushNotificationProvider>());
    // ... register other providers

    return hub;
});
```

### 2. Configuration Settings
```json
{
  "Notifications": {
    "SignalR": {
      "Enabled": true
    },
    "Push": {
      "Enabled": true
    },
    "SMS": {
      "Enabled": false
    },
    "Email": {
      "Enabled": false
    },
    "Telegram": {
      "Enabled": false,
      "ProxyBaseUrl": "",
      "BotToken": "",
      "TargetChatIds": [],
      "Commands": {
        "ApiToken": ""
      }
    }
  }
}
```

`TargetChatIds` is the allowlist for both outbound alerts and inbound commands (`/rates`). `Commands:ApiToken` is a shared secret for `POST /api/telegram/command`, used by the Telegram Serverless handler. Setup: `telegram-serverless/README.md`.

### 3. Client-Side Setup (_Layout.cshtml)
```javascript
// Set current user ID for client-side notification filtering
window.currentUserId = '@(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "")';
```

## File Structure

```
Services/Notifications/
├── README.md                           # This documentation
├── INotificationProvider.cs            # Base interface and shared types
├── NotificationHub.cs                  # Central coordination hub
└── Providers/
    ├── SignalRNotificationProvider.cs  # Real-time browser notifications
    ├── PushNotificationProvider.cs     # Web push notifications
    └── FutureNotificationProviders.cs  # SMS, Email, Telegram templates

wwwroot/js/
└── admin-notifications.js              # Client-side filtering logic

Views/Shared/
└── _Layout.cshtml                      # User ID injection for filtering
```

## Integration Points

### Controllers Using Notifications:
1. **OrdersController** - Order lifecycle notifications
2. **DatabaseController** - Manual transaction notifications
3. **AdminManagementController** - User management notifications
4. **AccountingDocumentsController** - Document workflow notifications

### Dependencies:
- **ASP.NET Core Identity** - User management and authentication
- **SignalR** - Real-time communication
- **Entity Framework** - Database operations
- **WebPush library** - Push notification delivery

## Adding New Notifications

### Step 1: Determine Event Type
- Use existing `NotificationEventType` if applicable
- Add new enum value if needed

### Step 2: Choose Method
- **Specific events**: Use typed methods (`SendOrderNotificationAsync`, etc.)
- **Custom events**: Use `SendManualAdjustmentNotificationAsync`

### Step 3: Controller Integration
```csharp
public class YourController : Controller
{
    private readonly INotificationHub _notificationHub;
    private readonly UserManager<ApplicationUser> _userManager;

    // In your action method:
    var currentUser = await _userManager.GetUserAsync(User);

    await _notificationHub.SendManualAdjustmentNotificationAsync(
        title: "Your Notification Title",
        message: "Your notification message",
        eventType: NotificationEventType.YourEventType,
        userId: currentUser?.Id, // Excludes current user from popups
        navigationUrl: "/your/target/url",
        priority: NotificationPriority.Normal
    );
```

### Step 4: Test Behavior
- Verify the performing user doesn't see SignalR popups
- Confirm other admin users receive real-time notifications
- Check push notifications are sent to all admins

## Troubleshooting

### Common Issues:

1. **User still sees their own notifications**:
   - Verify `UserManager<ApplicationUser>` is injected in controller
   - Ensure `currentUser?.Id` is passed to notification methods
   - Check `window.currentUserId` is set in _Layout.cshtml
   - Verify SignalR user groups are configured correctly

2. **Notifications not appearing for anyone**:
   - Check `SendToAllAdmins = true` is set in notification context
   - Verify provider is enabled in configuration
   - Check provider registration in Program.cs
   - Ensure SignalR connection is established

3. **Accounting document notifications not working**:
   - Verify `SendToAllAdmins = true` and `ExcludeUserIds` are set in context
   - Check SignalR provider includes `excludeUserIds` in notification data

4. **Push notifications failing**:
   - Verify VAPID keys are configured
   - Check user has active push subscriptions
   - Review push notification logs

### Debug Information:
- Notification Hub logs provider registration and sending attempts
- SignalR provider logs successful deliveries and user exclusions
- Push provider logs subscription management and delivery status
- Client-side console logs user exclusion checks

## Recent Fixes & Lessons Learned

### Critical Bug Fixes:
1. **Accounting Document Notifications Missing Properties** (Sep 2025):
   - **Issue**: Accounting document notifications weren't sent to other admins
   - **Root Cause**: Missing `SendToAllAdmins = true` and `ExcludeUserIds` in context
   - **Fix**: Added missing properties to `BuildAccountingDocumentNotificationContextAsync`

2. **Inconsistent User Exclusion Data** (Sep 2025):
   - **Issue**: Some notification types excluded users, others didn't
   - **Root Cause**: SignalR provider inconsistently included `excludeUserIds` in data
   - **Fix**: Standardized all providers to include exclusion data

3. **Client-Side Filtering Implementation** (Sep 2025):
   - **Issue**: Server-side exclusion wasn't working for SignalR popups
   - **Root Cause**: No client-side filtering logic
   - **Fix**: Added JavaScript user exclusion checks with `window.currentUserId`

### Key Architectural Decisions:
- **Server sends to all, client filters**: Push notifications go to everyone, SignalR popups are filtered client-side
- **User ID injection**: Global `window.currentUserId` enables client-side filtering
- **Consistent context building**: All notification contexts must include `SendToAllAdmins` and `ExcludeUserIds`

## Related Models

### Database Models:
- **Notification** (`Models/Notification.cs`) - Persistent notification storage
- **PushSubscription** - User push notification subscriptions
- **PushNotificationLog** - Delivery tracking and analytics

### DTOs and Context:
- **NotificationContext** - Runtime notification data
- **RelatedEntity** - Links notifications to business entities

---

*Last Updated: September 21, 2025*
*Version: 1.1 - Includes user exclusion fixes and accounting document notification fixes*