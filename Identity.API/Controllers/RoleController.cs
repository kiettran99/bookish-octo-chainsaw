using Common.Enums;
using Identity.API.Attributes;
using Identity.Domain.AggregatesModel.RoleAggregates;
using Identity.Domain.AggregatesModel.UserAggregates;
using Identity.Domain.Models.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(ERoles.Administrator)]
public class RoleController : ControllerBase
{
    private readonly RoleManager<Role> _roleManager;
    private readonly UserManager<User> _userManager;

    public RoleController(
        RoleManager<Role> roleManager,
        UserManager<User> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    [HttpGet]
    [Route("all")]
    [Authorize(ERoles.Administrator)]
    public async Task<IActionResult> GetAllRolesAsync()
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
        return Ok(roles);
    }

    [HttpPost]
    [Authorize(ERoles.Administrator)]
    public async Task<IActionResult> CreateRole([FromBody] RoleCreateRequestModel roleRequest)
    {
        if (string.IsNullOrEmpty(roleRequest.Name))
        {
            return BadRequest("Role name cannot be empty.");
        }

        var role = new Role(roleRequest.Name);
        var result = await _roleManager.CreateAsync(role);

        if (result.Succeeded)
        {
            return Ok(role);
        }

        return BadRequest(result.Errors);
    }

    [HttpPut("{id}")]
    [Authorize(ERoles.Administrator)]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] RoleUpdateRequestModel roleRequest)
    {
        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
        {
            return NotFound();
        }

        role.Name = roleRequest.Name;
        var result = await _roleManager.UpdateAsync(role);

        if (result.Succeeded)
        {
            return Ok(role);
        }

        return BadRequest(result.Errors);
    }

    [HttpDelete("{id}")]
    [Authorize(ERoles.Administrator)]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
        {
            return NotFound();
        }

        var result = await _roleManager.DeleteAsync(role);

        if (result.Succeeded)
        {
            return Ok(role);
        }

        return BadRequest(result.Errors);
    }

    #region User Roles
    [HttpGet("users/{userId}")]
    [Authorize(ERoles.User)]
    public async Task<IActionResult> GetRolesAsync([FromRoute] int userId)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return NotFound();
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        return Ok(userRoles);
    }

    [HttpPut("users/{userId}")]
    [Authorize(ERoles.Administrator)]
    public async Task<IActionResult> UpdateRoles([FromRoute] int userId, [FromBody] List<string> roles)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return NotFound();
        }

        var allRoles = await _roleManager.Roles.ToListAsync();
        var dbRoles = allRoles.ConvertAll(o => o.Name);

        // Check roles are valid
        var isValidRoles = roles.TrueForAll(dbRoles.Contains);
        if (!isValidRoles)
        {
            return BadRequest("Roles is invalid");
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        var rolesToAdd = roles.Except(userRoles);
        var rolesToRemove = userRoles.Except(roles);

        await _userManager.AddToRolesAsync(user, rolesToAdd);
        await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

        return Ok(roles);
    }
    #endregion
}