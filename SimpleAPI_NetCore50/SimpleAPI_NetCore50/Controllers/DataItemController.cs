using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpleAPI_NetCore50.Data;
using SimpleAPI_NetCore50.Models;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataItemController : Controller
    {
        private readonly SimpleApiContext DatabaseContext;

        public DataItemController(SimpleApiContext context)
        {
            DatabaseContext = context;
        }

        // GET: api/dataitem
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DataItem>>> GetRecords()
        {
            return await DatabaseContext.DataItems.ToListAsync();
        }

        // GET: api/dataitem/5
        [HttpGet("{id}")]
        public async Task<ActionResult<DataItem>> GetRecord(string id)
        {
            var dataItem = await DatabaseContext.DataItems.FindAsync(id);

            if (dataItem == null)
            {
                return NotFound();
            }

            return dataItem;
        }

        // PUT: api/dataitem/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRecord(string id, DataItem dataItem)
        {
            if (id != dataItem.Id)
            {
                return BadRequest();
            }

            DatabaseContext.Entry(dataItem).State = EntityState.Modified;

            try
            {
                await DatabaseContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.RecordExists(id))
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

        // POST: api/dataitem
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<DataItem>> PostRecord(DataItem dataItem)
        {
            DatabaseContext.DataItems.Add(dataItem);
            try
            {
                await DatabaseContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (this.RecordExists(dataItem.Id))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            return CreatedAtAction(nameof(GetRecord), new { id = dataItem.Id }, dataItem);
        }

        // DELETE: api/dataitem/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecord(string id)
        {
            var dataItem = await DatabaseContext.DataItems.FindAsync(id);
            if (dataItem == null)
            {
                return NotFound();
            }

            DatabaseContext.DataItems.Remove(dataItem);
            await DatabaseContext.SaveChangesAsync();

            return NoContent();
        }

        private bool RecordExists(string id)
        {
            return DatabaseContext.DataItems.Any(e => e.Id == id);
        }
    }
}
