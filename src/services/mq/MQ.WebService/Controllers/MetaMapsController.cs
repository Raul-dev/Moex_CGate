using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MQ.dal.Data;
using MQ.dal.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace MQ.WebService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MetaMapsController : ControllerBase
    {
        private readonly MetastorageContext _context;

        public MetaMapsController(MetastorageContext context)
        {
            _context = context;
        }

        // GET: api/MetaMaps
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Metamap>>> GetMetamaps()
        {
        
            return await _context.Metamaps.ToListAsync();
        }   

        // GET: api/MetaMaps/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Metamap>> GetMetamap(short id)
        {
            var metamap = await _context.Metamaps.FindAsync(id);

            if (metamap == null)
            {
                return NotFound();
            }

            return metamap;
        }

        // PUT: api/MetaMaps/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMetamap(short id, Metamap metamap)
        {
            if (id != metamap.MetamapId)
            {
                return BadRequest();
            }

            _context.Entry(metamap).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MetamapExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

/*
        [HttpPost]
        public async Task<IActionResult> NewsletterSignup(CollectionModel model)
        {
            //<= Error: is prompting prior to the body of this method being called, 
            //        and the parameter object being populated with post data!!!

            var newsletter = new Newsletter
            {
                Id = 0,
                Fname = model.Newsletter.Fname,
                Email = model.Newsletter.Email,
                Phone = model.Newsletter.Phone,
                Active = 0,
                GUID = Guid.NewGuid().ToString(),
                Create = DateTime.Now,
                Update = DateTime.Now
            };


        }
*/
        // DELETE: api/MetaMaps/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMetamap(short id)
        {
            var metamap = await _context.Metamaps.FindAsync(id);
            if (metamap == null || id < 4)
            {
                return NotFound();
            }

            _context.Metamaps.Remove(metamap);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MetamapExists(short id)
        {
            _context.Metamaps.FirstOrDefault(e => e.MetamapId == id);
            return _context.Metamaps.Any(e => e.MetamapId == id);
        }
    }
}
