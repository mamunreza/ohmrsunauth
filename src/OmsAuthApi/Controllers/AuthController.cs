﻿using Azure.Communication.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OmsAuthApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public AuthController(IConfiguration configuration, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
    {
        _configuration = configuration;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new IdentityUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var confirmationLink = Url.Action("confirmemail", "Auth",
                new { userId = user.Id, token = token }, Request.Scheme);

            await SendEmailAsync(user.Email, "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

            return Ok(new { token });
        }

        return BadRequest(result.Errors);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, false, false);

        if (result.Succeeded)
        {
            var token = GenerateJwtToken(request.Email);

            return Ok(new { token });
        }

        return Unauthorized();
    }

    [HttpPost("login2")]
    public IActionResult Login2([FromBody] LoginRequest request)
    {
        if (request.Email == "admin@oms.com" && request.Password == "admin")
        {
            var token = GenerateJwtToken(request.Email);
            return Ok(new { token });
        }

        return Unauthorized();
    }

    [HttpGet("confirmemail")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (userId == null || token == null)
        {
            return BadRequest("Invalid token");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userId}'.");
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            return Ok("Email confirmed successfully");
        }

        return BadRequest(result.Errors);
    }


    private string GenerateJwtToken(string username)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:Expires"])),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        string connectionString = _configuration["AzureEmail:Endpoint"];
        var emailClient = new EmailClient(connectionString);
        var emailContent = new EmailContent(subject)
        {
            PlainText = "OMS requests you to confirm Email",
            Html = htmlMessage
        };
        var senderAddress = _configuration["AzureEmail:SenderAddress"];  // Must be a verified domain
        var emailRecipients = new EmailRecipients(new[] { new EmailAddress(email) });
        var emailMessage = new EmailMessage(senderAddress, emailRecipients, emailContent);

        try
        {
            var sendResult = await emailClient.SendAsync(Azure.WaitUntil.Started, emailMessage);
            Console.WriteLine($"Email sent successfully. Message ID: {sendResult.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email: {ex.Message}");
        }
    }
}

