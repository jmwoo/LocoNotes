﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using loconotes.Models.Auth;
using loconotes.Models.User;
using System.IdentityModel.Tokens.Jwt;
using loconotes.Services;

namespace loconotes.Controllers
{
    [Route("api/auth")]
    public class AuthController : Controller
    {
        private readonly IJwtService _jwtService;
        private readonly ILoginService _loginService;

        public AuthController(
            IJwtService jwtService,
            ILoginService loginService
        )
        {

            _loginService = loginService;
            _jwtService = jwtService;
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] UserLogin userLogin)
        {
            var user = await _loginService.Login(userLogin);

            if (user == null)
            {
                return BadRequest("Invalid credentials");
            }

            var jwtResult = _jwtService.MakeJwt(user);

            return Ok(jwtResult);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("signup")]
        public async Task<IActionResult> Signup([FromBody] UserLogin userLogin)
        {
            throw new NotImplementedException();
        }
    }
}
