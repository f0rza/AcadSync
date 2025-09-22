using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AcadSync.Web.Services;
using AcadSync.Web.Models;
using System.Linq;

namespace AcadSync.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RulesController : ControllerBase
    {
        private readonly IRuleRepository _repo;

        public RulesController(IRuleRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rules = await _repo.GetAllAsync();
            return Ok(rules.Select(r => new
            {
                r.Id,
                r.Name,
                r.Slug,
                r.Description,
                r.IsActive,
                r.CurrentVersionId,
                r.CreatedAt,
                r.UpdatedAt
            }));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var rule = await _repo.GetByIdAsync(id);
            if (rule == null) return NotFound();
            return Ok(rule);
        }

        public class CreateRuleRequest
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = null!;
            public string Slug { get; set; } = null!;
            public string? Description { get; set; }
            public bool IsActive { get; set; } = true;
            public string? Yaml { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRuleRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Slug))
                return BadRequest("Name and Slug are required.");

            var entity = new RuleEntity
            {
                Id = req.Id ?? Guid.NewGuid(),
                Name = req.Name,
                Slug = req.Slug,
                Description = req.Description,
                IsActive = req.IsActive
            };

            var saved = await _repo.CreateOrUpdateRuleAsync(entity);

            if (!string.IsNullOrWhiteSpace(req.Yaml))
            {
                // validate YAML first
                if (!await _repo.ValidateYamlAsync(req.Yaml, out var error))
                {
                    return BadRequest(new { error });
                }

                var version = await _repo.AddVersionAsync(saved.Id, req.Yaml, User?.Identity?.Name ?? "web");
                saved.CurrentVersionId = version.Id;
            }

            return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
        }

        public class AddVersionRequest
        {
            public string Yaml { get; set; } = null!;
            public string? Notes { get; set; }
        }

        [HttpPost("{id:guid}/versions")]
        public async Task<IActionResult> AddVersion(Guid id, [FromBody] AddVersionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Yaml))
                return BadRequest("Yaml is required.");

            if (!await _repo.ValidateYamlAsync(req.Yaml, out var error))
                return BadRequest(new { error });

            var version = await _repo.AddVersionAsync(id, req.Yaml, User?.Identity?.Name ?? "web", req.Notes);
            return Ok(version);
        }

        public class ValidateYamlRequest
        {
            public string Yaml { get; set; } = null!;
        }

        [HttpPost("validate")]
        public async Task<IActionResult> ValidateYaml([FromBody] ValidateYamlRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Yaml))
                return BadRequest("Yaml is required.");

            var ok = await _repo.ValidateYamlAsync(req.Yaml, out var error);
            if (!ok)
                return BadRequest(new { error });

            return Ok(new { message = "YAML is valid" });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _repo.DeleteRuleAsync(id);
            return NoContent();
        }
    }
}
