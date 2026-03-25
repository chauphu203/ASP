using Microsoft.AspNetCore.Mvc;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private static List<Category> categories = new List<Category>
        {
            new Category { Id = 1, Name = "Electronics", Description = "Electronic devices" },
            new Category { Id = 2, Name = "Fashion", Description = "Clothing and accessories" },
            new Category { Id = 3, Name = "Books", Description = "All kinds of books" },
            new Category { Id = 4, Name = "Home Appliances", Description = "Household equipment" }
        };

        // GET: api/category
        [HttpGet]
        public ActionResult<IEnumerable<Category>> Get()
        {
            return Ok(categories);
        }

        // GET api/category/1
        [HttpGet("{id}")]
        public ActionResult<Category> Get(int id)
        {
            var category = categories.FirstOrDefault(c => c.Id == id);

            if (category == null)
                return NotFound();

            return Ok(category);
        }

        // POST api/category
        [HttpPost]
        public IActionResult Post([FromBody] Category category)
        {
            categories.Add(category);
            return Ok(category);
        }

        // PUT api/category/1
        [HttpPut("{id}")]
        public IActionResult Put(int id, [FromBody] Category updatedCategory)
        {
            var category = categories.FirstOrDefault(c => c.Id == id);

            if (category == null)
                return NotFound();

            category.Name = updatedCategory.Name;
            category.Description = updatedCategory.Description;

            return Ok(category);
        }

        // DELETE api/category/1
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var category = categories.FirstOrDefault(c => c.Id == id);

            if (category == null)
                return NotFound();

            categories.Remove(category);
            return Ok();
        }
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}