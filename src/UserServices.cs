using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public class AuthService
    {
        private readonly SupplyChainDbContext _context;

        public AuthService(SupplyChainDbContext context)
        {
            _context = context;
        }

        public User? Authenticate(string username, string password)
        {
            // In a real app, this would check hashed passwords
            var user = _context.Users
                .Include(u => u.Organization)
                .FirstOrDefault(u => u.Username == username && u.Password == password);

            return user;
        }

        public bool IsAuthorized(User user, UserRole[] allowedRoles)
        {
            return allowedRoles.Contains(user.Role);
        }

        public List<Notification> GetUserNotifications(int userId)
        {
            return _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedDate)
                .ToList();
        }

        public void MarkNotificationAsRead(int notificationId)
        {
            var notification = _context.Notifications.Find(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                _context.SaveChanges();
            }
        }

        public void AddNotification(Notification notification)
        {
            _context.Notifications.Add(notification);
            _context.SaveChanges();
        }

        public Organization? GetOrganizationById(int organizationId)
        {
            return _context.Organizations
                .Include(o => o.Users)
                .FirstOrDefault(o => o.Id == organizationId);
        }
        
        public bool UpdateOrganization(Organization organization)
        {
            if (organization == null)
                return false;
                
            // Ensure it exists in the database
            var exists = _context.Organizations.Any(o => o.Id == organization.Id);
            if (!exists)
                return false;
                
            // Update the organization
            _context.Organizations.Update(organization);
            return true;
        }
    }

    public class UserService
    {
        private readonly SupplyChainDbContext _context;

        public UserService(SupplyChainDbContext context)
        {
            _context = context;
        }

        public List<User> GetUsers()
        {
            return _context.Users
                .Include(u => u.Organization)
                .OrderBy(u => u.Organization != null ? u.Organization.Name : "")
                .ThenBy(u => u.Name)
                .ToList();
        }

        public User GetUserById(int id)
        {
            return _context.Users
                .Include(u => u.Organization)
                .FirstOrDefault(u => u.Id == id)
                ?? throw new Exception($"User with ID {id} not found");
        }

        public List<Organization> GetOrganizations()
        {
            return _context.Organizations
                .OrderBy(o => o.Name)
                .ToList();
        }

        public Organization GetOrganizationById(int id)
        {
            return _context.Organizations
                .FirstOrDefault(o => o.Id == id)
                ?? throw new Exception($"Organization with ID {id} not found");
        }

        public ServiceResult CreateUser(User user)
        {
            try
            {
                if (_context.Users.Any(u => u.Username == user.Username))
                    return ServiceResult.Failed($"Username {user.Username} already exists");

                if (user.OrganizationId.HasValue)
                {
                    var organization = _context.Organizations.Find(user.OrganizationId.Value);
                    if (organization == null)
                        return ServiceResult.Failed($"Organization with ID {user.OrganizationId.Value} not found");
                }

                _context.Users.Add(user);
                _context.SaveChanges();

                return ServiceResult.Successful($"User {user.Name} created successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Failed to create user: {ex.Message}");
            }
        }

        public ServiceResult CreateOrganization(Organization organization)
        {
            try
            {
                if (_context.Organizations.Any(o => o.Name == organization.Name))
                    return ServiceResult.Failed($"Organization with name {organization.Name} already exists");

                _context.Organizations.Add(organization);
                _context.SaveChanges();

                return ServiceResult.Successful($"Organization {organization.Name} created successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Failed to create organization: {ex.Message}");
            }
        }

        public List<User> GetOrganizationUsers(int organizationId)
        {
            return _context.Users
                .Where(u => u.OrganizationId == organizationId)
                .OrderBy(u => u.Name)
                .ToList();
        }

        public Organization AddOrganization(Organization organization)
        {
            _context.Organizations.Add(organization);
            _context.SaveChanges();
            return organization;
        }

        public User AddUser(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
            return user;
        }

        public ServiceResult UpdateOrganization(Organization organization)
        {
            try
            {
                _context.Organizations.Update(organization);
                _context.SaveChanges();
                return ServiceResult.Successful($"Organization {organization.Name} updated successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Failed to update organization: {ex.Message}");
            }
        }
    }
}
