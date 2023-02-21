﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using HotelListing.API.Contracts;
using HotelListing.API.Data;
using HotelListing.API.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace HotelListing.API.Repository;

public class AuthManager : IAuthManager
{
    public UserManager<ApiUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthManager> _logger;
    private readonly IMapper _mapper;
    private ApiUser _user;

    private const string _loginProvider = "HotelListingApi";
    private const string _refreshToken = "RefreshToken";

    public AuthManager(IMapper mapper, UserManager<ApiUser> userManager, IConfiguration configuration, ILogger<AuthManager> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<IEnumerable<IdentityError>> Register(ApiUserDto userDto)
    {
        _user = _mapper.Map<ApiUser>(userDto);
        _user.UserName = userDto.Email;

        var result = await _userManager.CreateAsync(_user, userDto.Password);

        if (result.Succeeded)
            await _userManager.AddToRoleAsync(_user, "User");

        return result.Errors;
    }

    public async Task<AuthResponseDto> Login(LoginDto loginDto)
    {
        _logger.LogInformation($"Looking for user with email: {loginDto.Email}");
        _user = await _userManager.FindByEmailAsync(loginDto.Email);
        var isValidUser = await _userManager.CheckPasswordAsync(_user, loginDto.Password);

        if (_user == null || isValidUser == false)
        {
            _logger.LogWarning($"User with email: {loginDto.Email} was not found");
            return null;
        }
        var token = await GenerateToken();
        _logger.LogInformation($"Token generated for email: {_user.Email} | Token {token}");
        return new AuthResponseDto()
        {
            Token = token,
            UserId = _user.Id,
            RefreshToken = await CreateRefreshToken()
        };
    }

    public async Task<string> CreateRefreshToken()
    {
        await _userManager.RemoveAuthenticationTokenAsync(_user, _loginProvider, _refreshToken);
        var newRefreshToken = await _userManager.GenerateUserTokenAsync(_user, _loginProvider,
            _refreshToken);
        var result =
            await _userManager.SetAuthenticationTokenAsync(_user, _loginProvider,
                _refreshToken, newRefreshToken);
        return newRefreshToken;
    }

    public async Task<AuthResponseDto> VerifyRefreshToken(AuthResponseDto request)
    {
        var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
        var tokenContent = jwtSecurityTokenHandler.ReadJwtToken(request.Token);
        var username = tokenContent.Claims.ToList().FirstOrDefault(q => q.Type == JwtRegisteredClaimNames.Sub)?.Value;
        _user = await _userManager.FindByNameAsync(username);

        if (_user == null || _user.Id != request.UserId)
            return null;
        var isValidRefreshToken = await _userManager.VerifyUserTokenAsync(_user, _loginProvider, _refreshToken,
            request.RefreshToken);

        if (isValidRefreshToken)
        {
            var token = await GenerateToken();
            return new AuthResponseDto()
            {
                Token = token,
                UserId = _user.Id,
                RefreshToken = await CreateRefreshToken()
            };
        }

        await _userManager.UpdateSecurityStampAsync(_user);
        return null;
    }

    private async Task<string> GenerateToken()
    {
        var sequritykey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]));
        
        var credentials = new SigningCredentials(sequritykey, SecurityAlgorithms.HmacSha256);
        
        var roles = await _userManager.GetRolesAsync(_user);
        var rolesClaims = roles.Select(x => new Claim(ClaimTypes.Role, x)).ToList();

        var userClaims = await _userManager.GetClaimsAsync(_user);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, _user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, _user.Email),
        }.Union(userClaims).Union(rolesClaims);

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.Today.AddMinutes(Convert.ToInt32(_configuration["JwtSettings:DurationInMinutes"])),
            signingCredentials: credentials
            
            );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    
}