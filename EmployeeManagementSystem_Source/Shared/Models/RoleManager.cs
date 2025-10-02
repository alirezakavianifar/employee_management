using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Shared.Models
{
    public class RoleManager
    {
        public Dictionary<string, Role> Roles { get; private set; } = new();

        public RoleManager()
        {
            InitializeDefaultRoles();
        }

        private void InitializeDefaultRoles()
        {
            // Add some default roles
            AddRole("manager", "مدیر", "مدیر سیستم", "#FF5722", 100);
            AddRole("supervisor", "سرپرست", "سرپرست تیم", "#FF9800", 80);
            AddRole("employee", "کارمند", "کارمند عادی", "#4CAF50", 50);
            AddRole("intern", "کارآموز", "کارآموز", "#2196F3", 30);
            AddRole("contractor", "پیمانکار", "پیمانکار", "#9C27B0", 20);
        }

        public bool AddRole(string roleId, string name, string description = "", string color = "#4CAF50", int priority = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(name))
                    return false;

                if (Roles.ContainsKey(roleId))
                    return false; // Role already exists

                var role = new Role(roleId, name, description, color, priority);
                Roles[roleId] = role;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool UpdateRole(string roleId, string? name = null, string? description = null, string? color = null, int? priority = null, bool? isActive = null)
        {
            try
            {
                if (!Roles.ContainsKey(roleId))
                    return false;

                var role = Roles[roleId];
                role.Update(name, description, color, priority, isActive);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteRole(string roleId)
        {
            try
            {
                if (!Roles.ContainsKey(roleId))
                    return false;

                // Don't allow deletion of default roles
                var defaultRoles = new[] { "manager", "supervisor", "employee", "intern", "contractor" };
                if (defaultRoles.Contains(roleId))
                    return false;

                Roles.Remove(roleId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Role? GetRole(string roleId)
        {
            return Roles.GetValueOrDefault(roleId);
        }

        public List<Role> GetAllRoles()
        {
            return Roles.Values.OrderByDescending(r => r.Priority).ThenBy(r => r.Name).ToList();
        }

        public List<Role> GetActiveRoles()
        {
            return Roles.Values.Where(r => r.IsActive).OrderByDescending(r => r.Priority).ThenBy(r => r.Name).ToList();
        }

        public bool RoleExists(string roleId)
        {
            return Roles.ContainsKey(roleId);
        }

        public int GetRoleCount()
        {
            return Roles.Count;
        }

        public string ToJson()
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    { "roles", Roles.Values.Select(r => r.ToDictionary()).Cast<object>().ToList() },
                    { "last_modified", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
                };
                return JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch
            {
                return "{}";
            }
        }

        public static RoleManager FromJson(string json)
        {
            try
            {
                var roleManager = new RoleManager();
                
                if (string.IsNullOrEmpty(json) || json == "{}")
                    return roleManager;

                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (data == null || !data.ContainsKey("roles"))
                    return roleManager;

                var rolesData = data["roles"];
                if (rolesData is List<object> rolesList)
                {
                    foreach (var roleObj in rolesList)
                    {
                        if (roleObj is Dictionary<string, object> roleDict)
                        {
                            var roleId = roleDict.GetValueOrDefault("role_id", "").ToString() ?? "";
                            var name = roleDict.GetValueOrDefault("name", "").ToString() ?? "";
                            var description = roleDict.GetValueOrDefault("description", "").ToString() ?? "";
                            var color = roleDict.GetValueOrDefault("color", "#4CAF50").ToString() ?? "#4CAF50";
                            var priority = 0;
                            if (roleDict.ContainsKey("priority") && int.TryParse(roleDict["priority"].ToString(), out int parsedPriority))
                                priority = parsedPriority;
                            var isActive = true;
                            if (roleDict.ContainsKey("is_active") && bool.TryParse(roleDict["is_active"].ToString(), out bool parsedIsActive))
                                isActive = parsedIsActive;

                            if (!string.IsNullOrEmpty(roleId) && !string.IsNullOrEmpty(name))
                            {
                                var role = new Role(roleId, name, description, color, priority);
                                role.IsActive = isActive;
                                
                                // Set creation/update times if available
                                if (roleDict.ContainsKey("created_at"))
                                {
                                    if (DateTime.TryParse(roleDict["created_at"].ToString(), out DateTime createdAt))
                                        role.CreatedAt = createdAt;
                                }
                                
                                if (roleDict.ContainsKey("updated_at"))
                                {
                                    if (DateTime.TryParse(roleDict["updated_at"].ToString(), out DateTime updatedAt))
                                        role.UpdatedAt = updatedAt;
                                }

                                roleManager.Roles[roleId] = role;
                            }
                        }
                    }
                }

                return roleManager;
            }
            catch
            {
                return new RoleManager();
            }
        }
    }
}
