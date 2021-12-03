using UnityEngine;
using System.Collections;
using SheepinatorCustom;
public class SheepNav : MonoBehaviour
{
    public S_State currentState;
    public SheepType sheepType;
    public Rigidbody rigidRef;

    public string CurrentState;

    public bool isdead = false;

    public bool isDead 
    {
        get
        {
            return isdead;
        }
        set
        {
            isdead = value;
            if(isdead)
            {
                SetState(new Dropping(this));
                StartCoroutine("destroyCountdown");
            }
        }
    }

    IEnumerator destroyCountdown()
    {
        yield return new WaitForSeconds(3);
        if(sheepType == SheepType.robot)
        {
            Game_Manager.TotalBadSheep--;
        }
        if(sheepType == SheepType.white)
        {
            Game_Manager.TotalWhiteSheep--;
        }
        Destroy(this.gameObject);
    }
    private Game_Manager _manager;
    public Game_Manager Manager
    {
        get
        {
            if(_manager == null)
            {
                _manager = GameObject.Find("GameManagerController").GetComponent<Game_Manager>();
            }
            return _manager;
        }
    }

    private void Start()
    {
        rigidRef = this.gameObject.GetComponent<Rigidbody>();

        if (gameObject.tag == "Sheep")
        {
            sheepType = SheepType.white;
        }
        else if (gameObject.tag == "BadSheep")
        {
            sheepType = SheepType.robot;
        }
        SetState(new Traveling(this));
    }

    private void Update()
    {
        if (currentState is Traveling)
        {
            currentState.Tick();
            CurrentState = currentState.ToString();
        }
    }

    void FixedUpdate()
    {
        if (currentState is Abducting || currentState is Dropping)
        {
            currentState.Tick();
            CurrentState = currentState.ToString();
        }
    }

    public void SetState(S_State state)
    {
        //Cannot change state when dead
        if (!isDead && !(currentState is MagnetStuck))
        {
            if (currentState != null)
                currentState.OnStateExit();

            currentState = state;
            currentState.OnStateEnter();
        }
    }

    //Trigger/Collision/State Setting Functions
    private void OnTriggerEnter(Collider other)
    {

        if (other.gameObject.tag == "AbductBoundary")
        {
            if (AbilityManager.isMagnetActive && gameObject.tag =="BadSheep")
            {
                SetState(new Abducting(this));
            }
            else if(!AbilityManager.isMagnetActive)
            {
                SetState(new Abducting(this));
            }
        }

        //REACHED THE SHIP
        else if (other.gameObject.tag == "ShipBoundary" && !isdead)
        {
            Manager.AbductedSheep(sheepType);
            UnityEngine.Object.Destroy(gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "AbductBoundary")
        {
            SetState(new Dropping(this));
        }
    }

    //Has hit the ground
    private void OnCollisionEnter(Collision other)
    {
        if(other.gameObject.tag == "Magnet" && gameObject.tag == "BadSheep")
        {
            SetState(new MagnetStuck(this, other));
        }

        if (other.gameObject.tag == "Ground")
        {
            SetState(new Traveling(this));
        }
    }
}

public abstract class S_State
{
    protected SheepNav sheepNav;
    public abstract void Tick();
    public abstract void OnStateEnter();
    public abstract void OnStateExit();
    public S_State(SheepNav sheepNav)
    {
        this.sheepNav = sheepNav;
    }

    //Used by Traveling and Dropping
    public Vector3 GetWayPoint()
    {
        //Get a new point
        Vector3 waypoint = Random.insideUnitSphere * 15;
        waypoint.y = 0;
        while(Vector3.Distance(waypoint, sheepNav.transform.position) < 3.0f)
        {
            waypoint = Random.insideUnitSphere * 15;
            waypoint.y = 0;
        }

        return waypoint;
    }
}

public class Abducting : S_State
{

    protected float abductSpeed;
    protected GameObject ship;

    public Abducting(SheepNav nav) : base(nav)
    {
        ship = GameObject.Find("ShipBoundary");
        abductSpeed = Game_Manager.AbductionSpeed;

    }

    public override void Tick()
    {
        sheepNav.transform.position = (Vector3.MoveTowards(sheepNav.transform.position, ship.transform.position, Time.deltaTime * abductSpeed));

        if (!AbilityManager.isMagnetActive)
        {
            if (sheepNav.gameObject.tag == "Sheep")
            {
                sheepNav.transform.RotateAround(sheepNav.transform.position, Vector3.up, -100 * Time.deltaTime);
                sheepNav.transform.RotateAround(ship.transform.position, Vector3.up, -200 * Time.deltaTime);
            }
            //Rotate opposite direction for bad sheep
            else if (sheepNav.gameObject.tag == "BadSheep")
            {
                sheepNav.transform.RotateAround(sheepNav.transform.position, Vector3.up, 100 * Time.deltaTime);
                sheepNav.transform.RotateAround(ship.transform.position, Vector3.up, 200 * Time.deltaTime);
            }
        }
    }

    public override void OnStateEnter()
    {
        sheepNav.rigidRef.useGravity = false;
        sheepNav.rigidRef.isKinematic = false;

        if(sheepNav.gameObject.tag == "BadSheep")
        {
            //slow robot sheep down
            abductSpeed = Game_Manager.AbductionSpeed / 1.5f;
        }
    }

    public override void OnStateExit()
    {

    }
}

public class MagnetStuck : S_State
{
    Collision magnetBarrier = null;

    public MagnetStuck(SheepNav nav, Collision other) : base(nav)
    {
        magnetBarrier = other;
    }

    public override void OnStateEnter()
    {
        //StickS
        sheepNav.gameObject.transform.parent = magnetBarrier.gameObject.transform;
        sheepNav.rigidRef.isKinematic = true;
        sheepNav.gameObject.GetComponent<Collider>().enabled = false;
        //AbilityManager.totalStuck++;


    }

    public override void OnStateExit()
    {
       
    }

    public override void Tick()
    {
        
    }
}




public class Traveling : S_State
{
    public float speed;
    public Vector3 currentPoint;


    public Traveling(SheepNav nav) : base(nav)
    {
        speed = 15f;
    }

    public override void Tick()
    {
        if (Vector3.Distance(sheepNav.transform.position, currentPoint) >= 0.3f)
        {

            Vector3 targetDirection = (currentPoint - sheepNav.transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            sheepNav.transform.rotation = Quaternion.RotateTowards(sheepNav.transform.rotation, targetRotation, 500.0f * Time.deltaTime);
            sheepNav.transform.position += sheepNav.transform.forward * Time.deltaTime * speed;
            
        }
        else
        {
            currentPoint = GetWayPoint();
        }
    }

    public override void OnStateEnter()
    {
        sheepNav.rigidRef.isKinematic = true;
        currentPoint = GetWayPoint();

        //Sheep is new to the world
        if (sheepNav.transform.position == Vector3.zero)
        {
            sheepNav.transform.position = currentPoint;
        }
    }

    public override void OnStateExit()
    {

    }
}

//Being dropped from the UFO
// OR
//Falling because of explosion
public class Dropping : S_State
{

    public Dropping(SheepNav nav) : base(nav)
    {

    }

    public override void Tick()
    {
        sheepNav.rigidRef.AddForce(-Vector3.up * 20.0f);
        if(sheepNav.rigidRef.velocity.magnitude > 10.0f)
        {
            sheepNav.rigidRef.velocity = Vector3.ClampMagnitude(sheepNav.rigidRef.velocity, 10.0f);
        }
    }

    public override void OnStateEnter()
    {
        sheepNav.rigidRef.isKinematic = false;
        sheepNav.rigidRef.useGravity = true;
    }

    public override void OnStateExit()
    {
        sheepNav.rigidRef.isKinematic = true;
        sheepNav.rigidRef.useGravity = false;
    }
}
