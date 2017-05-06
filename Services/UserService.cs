﻿using loconotes.Data;
using loconotes.Models.Profile;
using loconotes.Models.User;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace loconotes.Services
{
	public interface IUserService
	{
		Task UpdateProfile(ApplicationUser applicationUser, UpdateProfileModel updateProfileModel);
		Task<UserProfile> GetProfile(ApplicationUser appUser);
		Task UpdatePassword(ApplicationUser applicationUser, UpdatePasswordModel updatePasswordModel);
	}

    public class UserService : IUserService
    {
		private readonly LoconotesDbContext _dbContext;
	    private readonly ICryptoService _cryptoService;

		public UserService(
			LoconotesDbContext dbContext,
			ICryptoService cryptoService
		)
		{
			_dbContext = dbContext;
			_cryptoService = cryptoService;
		}

		public async Task UpdateProfile(ApplicationUser applicationUser, UpdateProfileModel updateProfileModel)
		{
			throw new NotImplementedException();
		}

	    public async Task UpdatePassword(ApplicationUser applicationUser, UpdatePasswordModel updatePasswordModel)
	    {
			if (updatePasswordModel == null)
				throw new ValidationException(nameof(updatePasswordModel));

		    var userDto = await _dbContext.Users.FindAsync(applicationUser.Id);

			if (userDto.PasswordHash != _cryptoService.Hash(updatePasswordModel.CurrentPassword))
		    {
			    throw new ValidationException("Current password is incorrect"); // TODO: somehow handle in controller and return BadRequest()
		    }

		    userDto.PasswordHash = _cryptoService.Hash(updatePasswordModel.NewPassword);

		    await _dbContext.SaveChangesAsync();
		}

		public async Task<UserProfile> GetProfile(ApplicationUser appUser)
		{
			var userDto = await _dbContext.Users.FindAsync(appUser.Id);
			
			// TODO: mapping
			return new UserProfile { Username = userDto.Username };
		}
	}
}
