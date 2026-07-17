using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ForexExchange.Models;
using ForexExchange.Services;
using TaskStatus = ForexExchange.Models.TaskStatus;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Services.Notifications;

namespace ForexExchange.Controllers
{
    [Authorize]
    public class TasksController : Controller
    {
        private readonly ITaskManagementService _taskService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationHub _notificationHub;

        public TasksController(
            ITaskManagementService taskService,
            UserManager<ApplicationUser> userManager,
            INotificationHub notificationHub
        )
        {
            _taskService = taskService;
            _userManager = userManager;
            _notificationHub = notificationHub;
        }

        // GET: Tasks
        public async Task<IActionResult> Index(string? assignedUserId = null, DateTime? dueDateFrom = null, DateTime? dueDateTo = null, TaskStatus? status = null)
        {
            var tasks = await _taskService.GetFilteredTasksAsync(assignedUserId, dueDateFrom, dueDateTo, status);
            
            // Populate filter dropdown data - only show Admin, Programmer, and Operator users
            var users = await _taskService.GetAvailableUsersAsync();
            ViewBag.Users = new SelectList(users, "Id", "FullName", assignedUserId);
            ViewBag.Statuses = new SelectList(Enum.GetValues<TaskStatus>(), status);
            
            // Keep current filter values for the view
            ViewBag.CurrentAssignedUserId = assignedUserId;
            ViewBag.CurrentDueDateFrom = dueDateFrom?.ToString("yyyy-MM-dd");
            ViewBag.CurrentDueDateTo = dueDateTo?.ToString("yyyy-MM-dd");
            ViewBag.CurrentStatus = status;

            return View(tasks);
        }

        // GET: Tasks/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var task = await _taskService.GetTaskByIdAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            return View(task);
        }

        // GET: Tasks/Create
        public async Task<IActionResult> Create()
        {
            var users = await _taskService.GetAvailableUsersAsync();
            ViewBag.AssignedToUserId = new SelectList(users, "Id", "FullName");
            return View();
        }

        // POST: Tasks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,DueDate,AssignedToUserId")] TaskItem task)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var createdTask = await _taskService.CreateTaskAsync(
                        task.Title,
                        task.Description,
                        task.DueDate,
                        task.AssignedToUserId
                    );
                    await _notificationHub.SendTaskNotificationAsync(
                        createdTask,
                        NotificationEventType.TaskAssignment,
                        _userManager.GetUserId(User)
                    );
                    TempData["Success"] = "وظیفه با موفقیت ایجاد شد.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"خطا در ایجاد وظیفه: {ex.Message}";
                }
            }

            var users = await _taskService.GetAvailableUsersAsync();
            ViewBag.AssignedToUserId = new SelectList(users, "Id", "FullName", task.AssignedToUserId);
            return View(task);
        }

        // GET: Tasks/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var task = await _taskService.GetTaskByIdAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            var users = await _taskService.GetAvailableUsersAsync();
            ViewBag.AssignedToUserId = new SelectList(users, "Id", "FullName", task.AssignedToUserId);
            return View(task);
        }

        // POST: Tasks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,DueDate,Status,AssignedToUserId")] TaskItem task)
        {
            if (id != task.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingTask = await _taskService.GetTaskByIdAsync(id);
                    if (existingTask == null)
                        return NotFound();

                    var oldStatus = existingTask.Status;
                    var oldAssignedUserId = existingTask.AssignedToUserId;
                    var updatedTask = await _taskService.UpdateTaskAsync(
                        id,
                        task.Title,
                        task.Description,
                        task.DueDate,
                        task.Status,
                        task.AssignedToUserId
                    );

                    var currentUserId = _userManager.GetUserId(User);
                    if (oldAssignedUserId != updatedTask.AssignedToUserId)
                    {
                        await _notificationHub.SendTaskNotificationAsync(
                            updatedTask,
                            NotificationEventType.TaskAssignment,
                            currentUserId,
                            oldStatus.ToString()
                        );
                    }

                    if (oldStatus != updatedTask.Status)
                    {
                        await _notificationHub.SendTaskNotificationAsync(
                            updatedTask,
                            updatedTask.Status == TaskStatus.Completed
                                ? NotificationEventType.TaskCompleted
                                : NotificationEventType.TaskProgress,
                            currentUserId,
                            oldStatus.ToString()
                        );
                    }
                    TempData["Success"] = "وظیفه با موفقیت به‌روزرسانی شد.";
                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException)
                {
                    return NotFound();
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"خطا در به‌روزرسانی وظیفه: {ex.Message}";
                }
            }

            var users = await _taskService.GetAvailableUsersAsync();
            ViewBag.AssignedToUserId = new SelectList(users, "Id", "FullName", task.AssignedToUserId);
            return View(task);
        }

        // POST: Tasks/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _taskService.DeleteTaskAsync(id);
                if (result)
                {
                    TempData["Success"] = "وظیفه با موفقیت حذف شد.";
                }
                else
                {
                    TempData["Error"] = "وظیفه یافت نشد.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در حذف وظیفه: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Tasks/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, TaskStatus status)
        {
            try
            {
                var currentUserId = _userManager.GetUserId(User);
                var task = await _taskService.GetTaskByIdAsync(id);
                var oldStatus = task?.Status;
                var result = await _taskService.UpdateTaskStatusAsync(id, status, currentUserId);
                if (result)
                {
                    if (task != null)
                    {
                        task.Status = status;
                        await _notificationHub.SendTaskNotificationAsync(
                            task,
                            status == TaskStatus.Completed
                                ? NotificationEventType.TaskCompleted
                                : NotificationEventType.TaskProgress,
                            currentUserId,
                            oldStatus?.ToString()
                        );
                    }
                    TempData["Success"] = "وضعیت وظیفه با موفقیت به‌روزرسانی شد.";
                }
                else
                {
                    TempData["Error"] = "شما اجازه تغییر وضعیت این وظیفه را ندارید یا وظیفه یافت نشد.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در به‌روزرسانی وضعیت: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}