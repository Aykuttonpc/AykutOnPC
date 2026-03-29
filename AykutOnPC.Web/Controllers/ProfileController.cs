using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ProfileController(IProfileService profileService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await profileService.GetOrCreateProfileAsync(cancellationToken);
        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateProfileDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Index", dto);

        var profile = new Profile
        {
            Id = dto.Id,
            FullName = dto.FullName,
            Title = dto.Title,
            Bio = dto.Bio,
            ProfilePictureUrl = dto.ProfilePictureUrl,
            GitHubUrl = dto.GitHubUrl,
            LinkedInUrl = dto.LinkedInUrl,
            TwitterUrl = dto.TwitterUrl,
            InstagramUrl = dto.InstagramUrl,
            Email = dto.Email
        };

        await profileService.UpdateProfileAsync(profile, cancellationToken);
        TempData["Success"] = "Profil başarıyla güncellendi!";
        return RedirectToAction(nameof(Index));
    }
}
