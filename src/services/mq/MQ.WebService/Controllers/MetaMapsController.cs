using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MQ.dal.Models;

namespace MQ.WebService.Controllers
{
    [Route("v1/mq/meta-maps")]
    [ApiController]
    public class MetaMapsController : ControllerBase
    {
        private readonly MetastorageContext _context;

        public MetaMapsController(MetastorageContext context)
        {
            _context = context;
        }

        // GET: v1/mq/meta-maps
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Metamap>>> GetMetamaps()
        {
        
            return await _context.Metamaps.ToListAsync();
        }   

        // GET: v1/mq/meta-maps/5
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

        // PUT: v1/mq/meta-maps/5
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

        // DELETE: v1/mq/meta-maps/5
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
