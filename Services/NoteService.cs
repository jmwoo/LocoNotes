﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using loconotes.Business.Exceptions;
using loconotes.Business.GeoLocation;
using loconotes.Data;
using loconotes.Models;
using loconotes.Models.Cache;
using loconotes.Models.Note;
using loconotes.Models.User;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace loconotes.Services
{
    public interface INoteService
    {
        Task<NoteViewModel> Create(ApplicationUser applicationUser, NoteCreateModel noteCreateModel);
        Task<NoteViewModel> Vote(ApplicationUser applicationUser, int NoteId, VoteModel voteModel);
        Task<IEnumerable<NoteViewModel>> Nearby(ApplicationUser applicationUser, NoteSearchRequest noteSearchRequest);
		Task DeleteAll(ApplicationUser applicationUser);
	    Task DeleteNote(ApplicationUser applicationUser, int noteId);
	    Task<IEnumerable<NoteViewModel>> GetNotesByUser(ApplicationUser applicationUser, string username);

    }

    public class NoteService : INoteService
    {
        private readonly LoconotesDbContext _dbContext;

        public NoteService(
            LoconotesDbContext dbContext
        )
        {
            _dbContext = dbContext;
        }

        public async Task<NoteViewModel> Create(ApplicationUser applicationUser, NoteCreateModel noteCreateModel)
        {
            var noteToCreate = noteCreateModel.ToNote(applicationUser);

            try
            {
                var createdEntity = _dbContext.Notes.Add(noteToCreate);

                await _dbContext.SaveChangesAsync().ConfigureAwait(false);

	            var note = (await _dbContext.Notes.Include(n => n.User).FirstAsync(n => n.Id == createdEntity.Entity.Id));
				return note.ToNoteViewModel(applicationUser, null);
			}
            catch (DbUpdateException updateException)
            {
                throw new ConflictException(updateException.Message, updateException);
            }
        }

		public async Task DeleteAll(ApplicationUser applicationUser)
		{
			foreach (var note in _dbContext.Notes.Where(note => note.UserId == applicationUser.Id))
			{
				note.IsDeleted = true;
			}
			await _dbContext.SaveChangesAsync().ConfigureAwait(false);
		}

	    public async Task DeleteNote(ApplicationUser applicationUser, int noteId)
	    {
		    var note = await _dbContext.Notes.FindAsync(noteId).ConfigureAwait(false);

		    if (note == null || note.IsDeleted)
		    {
			    throw new NotFoundException();
		    }

		    if (note.UserId != applicationUser.Id)
		    {
			    throw new UnauthorizedAccessException();
		    }

		    note.IsDeleted = true;
		    await _dbContext.SaveChangesAsync().ConfigureAwait(false);
		}

	    public async Task<IEnumerable<NoteViewModel>> GetNotesByUser(ApplicationUser applicationUser, string username)
	    {
		    var notes = _dbContext.Notes
				.Include(n => n.User)
				.Where(n => String.Equals(n.User.Username, username, StringComparison.CurrentCultureIgnoreCase))
				.Where(n => !n.IsDeleted)
				;

		    if (!String.Equals(applicationUser.Username, username, StringComparison.CurrentCultureIgnoreCase)) // if not looking up self
		    {
			    notes = notes.Where(n => !n.IsAnonymous);
		    }

		    return await ConvertToViewableNotes(applicationUser, await notes.ToListAsync());
		}

	    public async Task<NoteViewModel> Vote(ApplicationUser applicationUser, int NoteId, VoteModel voteModel)
        {
			var note = await _dbContext.Notes
				.Include(n => n.User)
				.FirstAsync(n => n.Id == NoteId)
				.ConfigureAwait(false);

	        if (note.IsDeleted)
	        {
		        throw new NotFoundException();
	        }

	        _dbContext.Votes.Add(new Vote
	        {
		        NoteId = note.Id,
		        UserId = voteModel.UserId,
		        Value = (int)voteModel.Vote
	        });

	        note.Score += Convert.ToInt32(voteModel.Vote);

	        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
	        return note.ToNoteViewModel(applicationUser, voteModel);
		}

        public async Task<IEnumerable<NoteViewModel>> Nearby(ApplicationUser applicationUser, NoteSearchRequest noteSearchRequest)
        {
            var geoCodeRange = GeolocationHelpers.CalculateGeoCodeRange(noteSearchRequest.LatitudeD, noteSearchRequest.LongitudeD, noteSearchRequest.RangeKmD,
                GeolocationHelpers.DistanceType.Kilometers);

	        var orderedNearbyNotes = await _dbContext.Notes
		        .Include(n => n.User)
		        .WhereInGeoCodeRange(new GeoCodeRange
		        {
			        MinimumLatitude = geoCodeRange.MinimumLatitude,
			        MaximumLatitude = geoCodeRange.MaximumLatitude,
			        MinimumLongitude = geoCodeRange.MinimumLongitude,
			        MaximumLongitude = geoCodeRange.MaximumLongitude,
		        })
		        .Where(n => !n.IsDeleted)
		        .OrderBy(n =>
			        GeolocationHelpers.CalculateDistance(
				        n.LatitudeD, n.LongitudeD,
				        noteSearchRequest.LatitudeD,
				        noteSearchRequest.LongitudeD,
				        GeolocationHelpers.DistanceType.Kilometers))
		        .ThenByDescending(n => n.Id)
				.Take(noteSearchRequest.Take)
				.ToListAsync();

	        return await ConvertToViewableNotes(applicationUser, orderedNearbyNotes);
        }

	    private async Task<List<NoteViewModel>> ConvertToViewableNotes(ApplicationUser applicationUser, List<Note> notes)
	    {
		    if (!notes.Any())
		    {
			    return new List<NoteViewModel>();
		    }

		    var votes = new List<Models.Vote>();

		    if (applicationUser.IsValid)
		    {
			    votes = await _dbContext.Votes.Where(v =>
				    notes.Select(n => n.Id).Contains(v.NoteId)
				    && v.UserId == applicationUser.Id
			    ).ToListAsync();
		    }

		    return notes
				.Where(n => !n.IsDeleted)
				.Select(n =>
				{
					var vote = votes.FirstOrDefault(v => v.NoteId == n.Id);

					VoteModel voteModel = null;
					if (vote != null)
					{
						voteModel = new VoteModel
						{
							UserId = applicationUser.Id,
							Vote = (VoteEnum)vote.Value
						};
					}

					return n.ToNoteViewModel(applicationUser, voteModel);
				}).ToList();
	    }
	}
}
