using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcadSync.Web.Data;
using AcadSync.Web.Models;
using Microsoft.EntityFrameworkCore;
using AcadSync.Processor.Utilities; // EprlLoader for validation

namespace AcadSync.Web.Services
{
    public interface IRuleRepository
    {
        event Action? RulesChanged;

        Task<List<RuleEntity>> GetAllAsync();
        Task<RuleEntity?> GetByIdAsync(Guid id);
        Task<RuleVersionEntity?> GetVersionAsync(Guid versionId);
        Task<RuleVersionEntity> AddVersionAsync(Guid ruleId, string yaml, string? createdBy = null, string? notes = null);
        Task<RuleEntity> CreateOrUpdateRuleAsync(RuleEntity rule);
        Task DeleteRuleAsync(Guid id);
        Task<bool> ValidateYamlAsync(string yaml, out string? error);
    }

    public class RuleRepository : IRuleRepository
    {
        private readonly AcadSyncWebDbContext _db;

        public event Action? RulesChanged;

        public RuleRepository(AcadSyncWebDbContext db)
        {
            _db = db;
        }

        public async Task<List<RuleEntity>> GetAllAsync()
        {
            return await _db.Rules
                .Include(r => r.Versions.OrderByDescending(v => v.CreatedAt))
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<RuleEntity?> GetByIdAsync(Guid id)
        {
            return await _db.Rules
                .Include(r => r.Versions.OrderByDescending(v => v.CreatedAt))
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<RuleVersionEntity?> GetVersionAsync(Guid versionId)
        {
            return await _db.RuleVersions.FindAsync(versionId);
        }

        public async Task<RuleVersionEntity> AddVersionAsync(Guid ruleId, string yaml, string? createdBy = null, string? notes = null)
        {
            var version = new RuleVersionEntity
            {
                RuleId = ruleId,
                Yaml = yaml,
                CreatedBy = createdBy,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _db.RuleVersions.Add(version);

            // update rule's CurrentVersionId and UpdatedAt
            var rule = await _db.Rules.FindAsync(ruleId);
            if (rule != null)
            {
                rule.CurrentVersionId = version.Id;
                rule.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            RulesChanged?.Invoke();

            return version;
        }

        public async Task<RuleEntity> CreateOrUpdateRuleAsync(RuleEntity rule)
        {
            var existing = await _db.Rules.FirstOrDefaultAsync(r => r.Id == rule.Id);
            if (existing == null)
            {
                rule.CreatedAt = DateTime.UtcNow;
                rule.UpdatedAt = DateTime.UtcNow;
                _db.Rules.Add(rule);
            }
            else
            {
                existing.Name = rule.Name;
                existing.Slug = rule.Slug;
                existing.Description = rule.Description;
                existing.IsActive = rule.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            RulesChanged?.Invoke();

            return rule;
        }

        public async Task DeleteRuleAsync(Guid id)
        {
            var rule = await _db.Rules.FindAsync(id);
            if (rule != null)
            {
                _db.Rules.Remove(rule);
                await _db.SaveChangesAsync();
                RulesChanged?.Invoke();
            }
        }

        public Task<bool> ValidateYamlAsync(string yaml, out string? error)
        {
            try
            {
                // Use existing EprlLoader to parse YAML and detect structural errors
                var doc = EprlLoader.LoadFromYaml(yaml);
                error = null;
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return Task.FromResult(false);
            }
        }
    }
}
